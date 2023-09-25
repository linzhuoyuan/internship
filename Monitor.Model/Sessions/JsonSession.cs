using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using GalaSoft.MvvmLight.Messaging;
using Newtonsoft.Json;
using QLNet;
using QuantConnect;
using QuantConnect.Orders;
using QuantConnect.Packets;

namespace Monitor.Model.Sessions
{
    public class JsonSession : ISession
    {
        protected readonly ISessionHandler sessionHandler;
        protected readonly IResultConverter resultConverter;
        protected readonly BackgroundWorker eternalQueueListener = new BackgroundWorker();
        private readonly BackgroundWorker _queueReader = new BackgroundWorker();
        private readonly Result _result = new Result();
        private readonly SynchronizationContext _syncContext;
        protected readonly ConcurrentQueue<Packet> packetQueue = new ConcurrentQueue<Packet>();
        protected readonly AutoResetEvent resetEvent = new AutoResetEvent(false);
        protected bool closeAfterCompleted;

        private SessionState _state = SessionState.Unsubscribed;
        private int[] _underlyingChangeSeq;
        private int[] _volChangeSeq;

        private readonly SecurityMatrix _matrix;
        private readonly object _lock = new object();
        private readonly Dictionary<string, Holding> _holdingCache;
        private readonly Dictionary<string, ImmediateOptionPriceModelResult[,]> _greeksCache;
        private readonly Dictionary<string, OptionPriceMarketData> _optionPriceCache;

        public JsonSession(ISessionHandler sessionHandler, IResultConverter resultConverter)
        {
            this.sessionHandler = sessionHandler;
            this.resultConverter = resultConverter;

            _matrix = new SecurityMatrix(process => new AnalyticEuropeanEngine(process));

            _holdingCache = new Dictionary<string, Holding>();
            _greeksCache = new Dictionary<string, ImmediateOptionPriceModelResult[,]>();
            _optionPriceCache = new Dictionary<string, OptionPriceMarketData>();
            _syncContext = SynchronizationContext.Current;
        }

        public void InitSeq()
        {
            const int min = -2;
            const int step = 1;
            _underlyingChangeSeq = new int[5];
            for (var i = 0; i < _underlyingChangeSeq.Length; i++)
            {
                _underlyingChangeSeq[i] = min + i * step;
            }

            _volChangeSeq = new int[5];
            for (var i = 0; i < _volChangeSeq.Length; i++)
            {
                _volChangeSeq[i] = min + i * step;
            }
        }

        public void Initialize()
        {
            //Allow proper decoding of orders.
            JsonConvert.DefaultSettings = () => new JsonSerializerSettings
            {
                Converters = { new OrderJsonConverter() }
            };

            Subscribe();
        }

        public void Shutdown()
        {
            Unsubscribe();
        }

        private void QueueReader(object sender, DoWorkEventArgs e)
        {
            while (!_queueReader.CancellationPending)
            {
                Thread.Sleep(1);
                // Check whether we can dequeue
                if (packetQueue.Count == 0) 
                    continue;
                if (packetQueue.TryDequeue(out var p))
                    HandlePacket(p);
            }
            resetEvent.Set();
        }

        private void HandlePacket(Packet packet)
        {
            switch (packet.Type)
            {
                case PacketType.LiveResult:
                    HandleLiveResultPacket(packet);
                    break;
                case PacketType.BacktestResult:
                    HandleBacktestResultPacket(packet);
                    break;
                case PacketType.Log:
                    HandleLogPacket(packet);
                    break;
                case PacketType.Debug:
                    HandleDebugPacket(packet);
                    break;
                case PacketType.OptionPriceMarketData:
                    HandleOptionPriceMarketData(packet);
                    break;
                case PacketType.CustomerChart:
                    HandleCustomerChartPacket(packet);
                    break;
                case PacketType.GreeksChart:
                    HandleGreeksChartPacket(packet);
                    break;
                case PacketType.GreeksPnl:
                    HandleGreekPnlDataPacket(packet);
                    break;
                case PacketType.TradeRecord:
                    HandleTradeRecordPacket(packet);
                    break;
            }
        }

        private static void HandleGreekPnlDataPacket(Packet packet)
        {
            var holdingPnlChartModel = (GreeksGnlChartDataPacket)packet;
            if (holdingPnlChartModel.Results.Count > 0)
            {
                var pnl = holdingPnlChartModel.Results[^1];
                var pnlJson = JsonConvert.SerializeObject(pnl);
                Messenger.Default.Send(pnlJson, "pnl");
            }
        }

        /// <summary>
        /// Send the Volatility packet
        /// </summary>
        /// <param name="packet"></param>
        private void HandleCustomerChartPacket(Packet packet)
        {
            var customerChartModel = (CustomerChartDataPacket)packet;
            Messenger.Default.Send(customerChartModel.Results, "CustomerChart");
        }

        private void HandleGreeksChartPacket(Packet packet)
        {
            var greeksChartModel = (GreeksChartDataPacket)packet;
            Messenger.Default.Send(greeksChartModel.Results, "greeks");
        }

        private void HandleTradeRecordPacket(Packet packet)
        {
            var tradeRecordModel = (TradeRecordPacket)packet;
            Messenger.Default.Send(tradeRecordModel, "trade");
        }

        private void HandleDebugPacket(Packet packet)
        {
            var debugEventModel = (DebugPacket)packet;
            _syncContext.Send(o => sessionHandler.HandleLogMessage(debugEventModel.Message, LogItemType.Debug), null);
        }

        private void HandleLogPacket(Packet packet)
        {
            var logEventModel = (LogPacket)packet;
            _syncContext.Send(o => sessionHandler.HandleLogMessage(logEventModel.Message, LogItemType.Log), null);
        }

        private void HandleBacktestResultPacket(Packet packet)
        {
            var backtestResultEventModel = (BacktestResultPacket)packet;
            var backtestResultUpdate = resultConverter.FromBacktestResult(backtestResultEventModel.Results);
            _result.Add(backtestResultUpdate);

            //添加持仓快照
            lock (_lock)
            {
                foreach (var item in backtestResultEventModel.Results.Holdings)
                {
                    _holdingCache[item.Key] = item.Value.Clone();
                }
            }

            if (backtestResultEventModel.Results.Holdings.Count > 0)
            {
                var holdingJson = JsonConvert.SerializeObject(backtestResultEventModel.Results.Holdings.Values);
                if (!string.IsNullOrEmpty(holdingJson))
                {
                    Messenger.Default.Send(holdingJson, "Holding");
                }
            }

            var context = new ResultContext
            {
                Name = Name,
                Result = _result,
                Progress = backtestResultEventModel.Progress
            };
            _syncContext.Send(o => sessionHandler.HandleResult(context), null);

            if (backtestResultEventModel.Progress == 1 && closeAfterCompleted)
            {
                _syncContext.Send(o => Unsubscribe(), null);
            }
        }

        private void HandleLiveResultPacket(Packet packet)
        {
            var liveResultEventModel = (LiveResultPacket)packet;
            var liveResultUpdate = resultConverter.FromLiveResult(liveResultEventModel.Results);
            _result.Add(liveResultUpdate);

            lock (_lock)
            {
                foreach (var item in liveResultEventModel.Results.Holdings)
                {
                    _holdingCache[item.Key] = item.Value.Clone();
                }
            }

            if (liveResultEventModel.Results.Holdings.Count > 0)
            {
                var holdingJson = JsonConvert.SerializeObject(liveResultEventModel.Results.Holdings.Values);
                if (!string.IsNullOrEmpty(holdingJson))
                {
                    Messenger.Default.Send(holdingJson, "Holding");
                }
            }

            var context = new ResultContext
            {
                Name = Name,
                Result = _result
            };

            _syncContext.Send(o => sessionHandler.HandleResult(context), null);
        }

        private void HandleOptionPriceMarketData(Packet packet)
        {
            var optionPriceEventModel = (OptionPriceMarketDataPacket)packet;
            foreach (var item in optionPriceEventModel.Results)
            {
                Evaluate(item);
            }
        }

        private void Evaluate(OptionPriceMarketData packet)
        {
            var needCalculate = false;
            if (_optionPriceCache.ContainsKey(packet.Symbol))
            {
                if ((packet.Time - _optionPriceCache[packet.Symbol].Time).TotalSeconds >= 60)
                {
                    _optionPriceCache[packet.Symbol] = packet;
                    needCalculate = true;
                }
            }
            else
            {
                _optionPriceCache[packet.Symbol] = packet;
                needCalculate = true;
            }

            lock (_lock)
            {
                if (_holdingCache.ContainsKey(packet.Symbol) && Math.Abs(_holdingCache[packet.Symbol].Quantity) > 0 && needCalculate)
                {
                    var result = _matrix.EvaluateMatrix(packet, _underlyingChangeSeq, _volChangeSeq);
                    _greeksCache[packet.Symbol] = result;
                    var sum = new ImmediateOptionPriceModelResult[_underlyingChangeSeq.Length, _volChangeSeq.Length];
                    foreach (var holding in _holdingCache)
                    {
                        if (holding.Key == "Total") continue;

                        if (_greeksCache.ContainsKey(holding.Value.Symbol.Value))
                        {
                            var matrix = _greeksCache[holding.Value.Symbol.Value];
                            for (var i = 0; i < _underlyingChangeSeq.Length; i++)
                            {
                                for (var j = 0; j < _volChangeSeq.Length; j++)
                                {
                                    sum[i, j].Greeks.Delta = sum[i, j].Greeks.Delta + matrix[i, j].Greeks.Delta * Math.Abs(holding.Value.Quantity);
                                    sum[i, j].Greeks.Gamma = sum[i, j].Greeks.Gamma + matrix[i, j].Greeks.Gamma * Math.Abs(holding.Value.Quantity);
                                    sum[i, j].Greeks.Vega = sum[i, j].Greeks.Vega + matrix[i, j].Greeks.Vega * Math.Abs(holding.Value.Quantity);
                                    sum[i, j].Greeks.Theta = sum[i, j].Greeks.Theta + matrix[i, j].Greeks.Theta * Math.Abs(holding.Value.Quantity);
                                    sum[i, j].Greeks.Rho = sum[i, j].Greeks.Rho + matrix[i, j].Greeks.Rho * Math.Abs(holding.Value.Quantity);
                                }
                            }
                            Messenger.Default.Send(sum, "matrix");
                        }
                    }
                }
            }
        }

        public void Subscribe()
        {
            try
            {
                // Configure the worker threads
                eternalQueueListener.WorkerSupportsCancellation = true;
                eternalQueueListener.DoWork += DoWork;
                eternalQueueListener.RunWorkerAsync();

                _queueReader.WorkerSupportsCancellation = true;
                _queueReader.DoWork += QueueReader;
                _queueReader.RunWorkerAsync();

                State = SessionState.Subscribed;
            }
            catch (Exception e)
            {
                throw new Exception("Could not subscribe to the stream", e);
            }
        }

        public void Unsubscribe()
        {
            try
            {
                if (eternalQueueListener != null)
                {
                    eternalQueueListener.CancelAsync();
                    eternalQueueListener.DoWork -= DoWork;
                    //_resetEvent.WaitOne();
                }

                if (_queueReader != null)
                {
                    _queueReader.CancelAsync();
                    _queueReader.DoWork -= QueueReader;
                    //_resetEvent.WaitOne();
                }

                State = SessionState.Unsubscribed;
            }
            catch (Exception e)
            {
                throw new Exception("Could not unsubscribe from the stream", e);
            }

        }

        protected virtual void DoWork(object sender, DoWorkEventArgs e)
        {
        }

        public SessionState State
        {
            get => _state;
            private set
            {
                _state = value;
                sessionHandler.HandleStateChanged(value);
            }
        }

        public bool CanSubscribe { get; } = true;

        public virtual string Name => throw new NotImplementedException();

        protected void ProcessPacket(string payload)
        {
            var packet = JsonConvert.DeserializeObject<Packet>(payload);
            switch (packet.Type)
            {
                case PacketType.LiveResult:
                    var liveResultEventModel = JsonConvert.DeserializeObject<LiveResultPacket>(payload);
                    packetQueue.Enqueue(liveResultEventModel);
                    break;
                case PacketType.BacktestResult:
                    var backtestResultEventModel = JsonConvert.DeserializeObject<BacktestResultPacket>(payload);
                    packetQueue.Enqueue(backtestResultEventModel);
                    break;
                case PacketType.Log:
                    var logEventModel = JsonConvert.DeserializeObject<LogPacket>(payload);
                    packetQueue.Enqueue(logEventModel);
                    break;
                case PacketType.OptionPriceMarketData:
                    var logOptionPriceModel = JsonConvert.DeserializeObject<OptionPriceMarketDataPacket>(payload);
                    packetQueue.Enqueue(logOptionPriceModel);
                    break;
                case PacketType.CustomerChart:
                    var customerChartModel = JsonConvert.DeserializeObject<CustomerChartDataPacket>(payload);
                    packetQueue.Enqueue(customerChartModel);
                    break;
                case PacketType.GreeksChart:
                    var greeksChartModel = JsonConvert.DeserializeObject<GreeksChartDataPacket>(payload);
                    packetQueue.Enqueue(greeksChartModel);
                    break;
                case PacketType.GreeksPnl:
                    var greeksPnlModel = JsonConvert.DeserializeObject<GreeksGnlChartDataPacket>(payload);
                    packetQueue.Enqueue(greeksPnlModel);
                    break;
                case PacketType.TradeRecord:
                    var trade = JsonConvert.DeserializeObject<TradeRecordPacket>(payload);
                    packetQueue.Enqueue(trade);
                    break;
            }
        }
    }
}