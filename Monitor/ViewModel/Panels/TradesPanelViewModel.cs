using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Messaging;
using Monitor.Model;
using Monitor.Model.Messages;
using QuantConnect.Orders;
using QuantConnect.Packets;

namespace Monitor.ViewModel.Panels
{
    public class TradesPanelViewModel : ToolPaneViewModel
    {
        private readonly IMessenger _messenger;

        //private ObservableCollection<Order> _orders = new ObservableCollection<Order>();

        //public ObservableCollection<Order> Orders
        //{
        //    get { return _orders; }
        //    set
        //    {
        //        _orders = value;
        //        RaisePropertyChanged();
        //    }
        //}
        private ObservableCollection<TradeRecord> _orders = new ObservableCollection<TradeRecord>();

        public ObservableCollection<TradeRecord> Orders
        {
            get { return _orders; }
            set
            {
                _orders = value;
                RaisePropertyChanged();
            }
        }
        public TradesPanelViewModel()
        {
        }
        public TradesPanelViewModel(IMessenger messenger)
        {
            //Name = "Trades";
            Name = Application.Current.TryFindResource("trades") as string;

            _messenger = messenger;
            //_messenger.Register<SessionUpdateMessage>(this, message =>
            //{
            //    ParseResult(message.ResultContext.Result);
            //});

            _messenger.Register<TradeRecordPacket>(this, "trade", message => {
                ParseResult(message.Trade);
            });
            _messenger.Register<SessionClosedMessage>(this, m => Clear());
        }

        private void Clear()
        {
            Orders.Clear();
        }

        //private void ParseResult(Result result)
        //{
        //    Orders = new ObservableCollection<Order>(result.Orders.OrderBy(o => o.Key).Select(p => p.Value));
        //}

        private void ParseResult(TradeRecord trade)
        {
            Application.Current?.Dispatcher.Invoke(() => {
                trade.TradeValue = trade.Price * trade.Amount;
                Orders.Add(trade);
            });            
        }
    }
}