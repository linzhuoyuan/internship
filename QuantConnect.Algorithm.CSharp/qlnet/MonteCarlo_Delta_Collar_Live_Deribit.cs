/*
 * QUANTCONNECT.COM - Democratizing Finance, Empowering Individuals.
 * Lean Algorithmic Trading Engine v2.0. Copyright 2014 QuantConnect Corporation.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 *
*/

using System;
using System.Collections.Generic;
using System.Linq;
using QuantConnect.Data;
using QuantConnect.Data.Market;
using QuantConnect.Orders;
using QuantConnect.Interfaces;
using QuantConnect.Securities.Option;
using System.IO;
using Newtonsoft.Json;
using QuantConnect.Securities;
using Accord.Math;
using Newtonsoft.Json.Linq;
using QuantConnect.Algorithm.CSharp.qlnet.tools;
using QuantConnect.Algorithm.CSharp.qlnet.AlgoTrade;

namespace QuantConnect.Algorithm.CSharp
{
    /// <summary>
    /// This example demonstrates how to add options for a given underlying equity security.
    /// It also shows how you can prefilter contracts easily based on strikes and expirations, and how you
    /// can inspect the option chain to pick a specific option contract to trade.
    /// </summary>
    /// <meta name="tag" content="using data" />
    /// <meta name="tag" content="options" />
    /// <meta name="tag" content="filter selection" />


    public class MonteCarlo_Delta_Collar_Live_Deribit : QCAlgorithm, IRegressionAlgorithmDefinition
    {
        //交易的币种
        private readonly List<string> _coinList = new List<string> { "BTC-PERPETUAL", "ETH-PERPETUAL" };

        //};
        //存储收盘价的字典
        public static Dictionary<string, List<decimal>> price_dict = new Dictionary<string, List<decimal>> { };
        //生成qc的symbol字典
        private readonly Dictionary<string, Symbol> _symbolDict = new Dictionary<string, Symbol> { };
        //上次运行时间字典
        public Dictionary<string, DateTime> lastrunning_time_list = new Dictionary<string, DateTime> { };
        //上次存储收盘价时间字典
        public Dictionary<string, DateTime> lastclose_time_list = new Dictionary<string, DateTime> { };
        //第一次运行字典
        public Dictionary<string, bool> first_run_list = new Dictionary<string, bool> { };
        //当前价格字典
        public Dictionary<string, decimal> underlying_price_list = new Dictionary<string, decimal> { };

        //每日运行通知时间
        public static TimeSpan timeSpans = new DateTime(1, 1, 1, 17, 02, 0).TimeOfDay;
        //存储收盘价时间
        public static TimeSpan closeTime = new DateTime(1, 1, 1, 23, 59, 0).TimeOfDay;

        //存储信息的文件
        public string jsonpath = Path.GetDirectoryName(typeof(QCAlgorithm).Assembly.Location) + "/Collar_Delta_Info_Deribit.json";
        //账户名称
        public string account_name = "fifi_deribit";

        //序列化的时间，重启时为当前时间
        public static DateTime lastsaveinfotime = DateTime.Now;
        //存储当前策略的数组
        public List<AddOneStrategy> AddOneStrategies = new List<AddOneStrategy>() { };

        //期权链大部分读取完成
        public bool tradable = true;
        //订单管理字典
        public Dictionary<string, OrderManager> om_list = new Dictionary<string, OrderManager> { };
        //仓位管理字典
        public Dictionary<string, PositionManager> pm_list = new Dictionary<string, PositionManager> { };

        //管理twap订单的list
        public static List<Twap> TwapList = new List<Twap> { };

        public override void Initialize()
        {
            //针对每个品种
            foreach (string coin in _coinList)
            {
                //生成qc的symbol格式
                _symbolDict[coin] = QuantConnect.Symbol.Create(
                    coin,
                    SecurityType.Future,
                    Market.Deribit
                );
                //订阅期货数据
                var future_contracts = FutureChainProvider.GetFutureContractList(_symbolDict[coin], DateTime.Now);
                foreach (var symbol in future_contracts)
                {
                    if (symbol.Value.Contains(coin))
                    {
                        AddFutureContract(symbol, Resolution.Tick);
                    }
                }
                //上次运行时间设置
                lastrunning_time_list[coin] = DateTime.Today.AddDays(-1).AddHours(16).AddMinutes(2);//new DateTime(2020, 08, 10, 16, 02, 0);//startDate.AddDays(-1);
                lastclose_time_list[coin] = DateTime.Today.AddDays(-1).AddHours(23).AddMinutes(59);//new DateTime(2020, 08, 10, 16, 02, 0);


                //第一次运行设置
                first_run_list[coin] = true;
                //记录最新价设置
                underlying_price_list[coin] = 0;
                price_dict[coin] = new List<decimal> { };

            }

            AddCrypto("USDTUSD", Resolution.Tick, Market.Binance);
            SetTimeZone(TimeZones.Shanghai);

            //设置历史数据缓存时间
            SetWarmUp(TimeSpan.FromDays(16), Resolution.Daily);


            //将保存的信息反序列化到AddOneStrategies中
            if (File.Exists(jsonpath))
            {
                string json_list = File.ReadAllText(jsonpath);
                AddOneStrategies = JsonConvert.DeserializeObject<List<AddOneStrategy>>(json_list);
            }

        }

        // [JsonConverter(typeof(AddOneStrategyJsonConverter))]
        [JsonObject(MemberSerialization.OptOut)]
        public class AddOneStrategy
        {
            //是否选出期权
            public bool hasopt = false;
            //对冲开始时的持仓，对冲的手数，从json文件读取
            public decimal balance_now = 0.0m;
            //是否突破upline
            public bool iscrossupline = false;
            //品种
            public string symbol = null;
            //账户
            public string account = null;
            //交易比例
            public decimal ratio = 0.0m;

            //对冲开始时的价格，put的strike
            public decimal underlying_price_now = 0;
            //上线，call的strike
            public decimal upline_now = 0;
            //到期日
            public DateTime expirydatenow = new DateTime(2022, 01, 01, 16, 02, 0);
            //上次下单时间
            public DateTime last_submit_order_time = DateTime.Now;
            //上次预警时间
            public DateTime last_warning_time = DateTime.Now;
            //当前delta值
            public decimal delta_now = 0.0m;
            //stoplimit上线
            public decimal stoplimit_upline = 0;
            //stoplimit下线
            public decimal stoplimit_lowline = 0;
            //是否可以下twap
            public bool twap_tradable = false;
            public AddOneStrategy(string symbol, string account, decimal ratio)
            {
                this.symbol = symbol;
                this.account = account;
                this.ratio = ratio;
            }
            [JsonIgnore]
            public ISyntheticOptionPriceModel _priceModel = SyntheticOptionPriceModels.MonteCarlo();

            /*public AddOneStrategy(bool hasopt, decimal balance_now, bool iscrossupline, string symbol, string account,
                decimal ratio, decimal underlying_price_now, decimal upline_now, DateTime expirydatenow,
                DateTime last_submit_order_time, DateTime last_warning_time, decimal delta_now,
                decimal stoplimit_upline, decimal stoplimit_lowline, bool twap_tradable,
                ISyntheticOptionPriceModel priceModel)
            {
                this.hasopt = hasopt;
                this.balance_now = balance_now;
                this.iscrossupline = iscrossupline;
                this.symbol = symbol;
                this.account = account;
                this.ratio = ratio;
                this.underlying_price_now = underlying_price_now;
                this.upline_now = upline_now;
                this.expirydatenow = expirydatenow;
                this.last_submit_order_time = last_submit_order_time;
                this.last_warning_time = last_warning_time;
                this.delta_now = delta_now;
                this.stoplimit_lowline = stoplimit_lowline;
                this.stoplimit_upline = stoplimit_upline;
                this.twap_tradable = twap_tradable;
                _priceModel = priceModel;
            } */

            public void DetectionStrategy(Slice slice, QCAlgorithm algo, FuturesChain future_chain, OrderManager om, PositionManager pm)
            {
                FuturesContract perpetual_contract = null;
                foreach (var item in future_chain)
                {
                    if (item.Symbol.Value.Contains(symbol))
                    {
                        perpetual_contract = item;
                        //System.Diagnostics.Debug.WriteLine($"{item.Symbol.Value} {item.Time.ToString("yyyy-MM-dd hh:mm:ss")}");
                        break;
                    }
                }


                //如果本币种有twap还在执行，禁止下新的twap
                var twap_pending_for_this_symbol = TwapList.Where(x => x.symbol.Contains(symbol));
                if (twap_pending_for_this_symbol.Count() > 0)
                {
                    twap_tradable = false;
                }
                else
                {
                    twap_tradable = true;
                }



                //读取实时价格
                decimal underlying_price = future_chain.Ticks.Values.FirstOrDefault().LastOrDefault().Price;
                if (underlying_price == 0 || perpetual_contract == null)
                {
                    return;
                }
                //当第一次运行或到期日，更新持仓，及本周期对冲期权手数
                if (balance_now == 0 || (DateTime.Now.Date == expirydatenow.Date && DateTime.Now.TimeOfDay >= new DateTime(2020, 01, 01, 16, 02, 0).TimeOfDay))
                {
                    hasopt = false;
                    //balance_now = algo.Portfolio.TotalPortfolioValue / underlying_price*ratio;
                    //balance_now = 0.01m;
                }
                //选择对冲的期权属性
                if (!hasopt && price_dict[symbol].Count() >= 15)
                {
                    OrderMessage odm = new OrderMessage();
                    odm.SendMessage(symbol + "Deribit_Delta 到期换月", "MonteCarloDelta");
                    hasopt = true;
                    underlying_price_now = underlying_price;
                    expirydatenow = DateTime.Now.Date.AddDays(10);
                    upline_now = underlying_price_now * 1.05m;
                    iscrossupline = false;
                }
                if (hasopt)
                {
                    //当前标的价格
                    double S = Convert.ToDouble(underlying_price);
                    //put的strike price，平直put
                    double X = Convert.ToDouble(underlying_price_now);
                    //call的strike price
                    double X_call = Convert.ToDouble(upline_now);
                    //利率
                    double r = 0.07;
                    //根据历史数据计算波动率
                    List<decimal> ret_list = new List<decimal> { };
                    for (int i = 1; i < price_dict[symbol].Count; i++)
                    {
                        ret_list.Add(
                            Convert.ToDecimal(Math.Log(Convert.ToDouble(price_dict[symbol][i] / price_dict[symbol][i - 1]), Math.E))
                        );
                    }

                    double sigma = Math.Sqrt(Convert.ToDouble(Var(ret_list))) * Math.Sqrt(365);
                    if (sigma == 0)
                    {
                        sigma = 0.7;
                    }
                    //距离到期日
                    double t = (expirydatenow.Date - DateTime.Now.Date).TotalDays / 365;
                    if (!iscrossupline)
                    {
                        if (t > 0)
                        {
                            var callPrice = _priceModel.Evaluate(S, X_call, 0, r, sigma, expirydatenow, EPutCall.Call);
                            var putPrice = _priceModel.Evaluate(S, X, 0, r, sigma, expirydatenow, EPutCall.Put);
                            //call的delta
                            var delta_call = callPrice.Greeks.Delta;
                            //put的detla
                            var delta_put = putPrice.Greeks.Delta;

                            //组合的detla，卖2call，买1put，持有1现货
                            var target_delta = -2 * delta_call + delta_put + 1;
                            var delta_up = (decimal)target_delta + 0.2m;
                            var delta_down = (decimal)target_delta - 0.2m;

                            //查询现货持仓
                            decimal holding_underlying_vol = 0;
                            var holding_underlying = algo.Portfolio.Securities.Values.Where(
                                x =>
                                    x.HoldStock == true && x.Symbol.SecurityType == SecurityType.Future && x.Symbol.Value.Contains(symbol)
                            );

                            if (holding_underlying.Count() > 0)
                            {
                                holding_underlying_vol = holding_underlying.FirstOrDefault().Holdings.Quantity;
                            }

                            var cash_delta = holding_underlying_vol;

                            //超出对冲带，进行对冲
                            if (cash_delta > delta_up * balance_now || cash_delta < delta_down * balance_now)
                            {

                                //var trade_vol = Math.Round((decimal)target_delta * balance_now - cash_delta, precision[symbol]);

                                //var price = trade_vol > 0 ? perpetual_contract.AskPrice : perpetual_contract.BidPrice;

                                var lotsize = perpetual_contract.Symbol.SymbolProperties.LotSize;
                                var minprice = perpetual_contract.Symbol.SymbolProperties.MinimumPriceVariation;

                                var trade_vol =
                                    Math.Floor(((decimal)target_delta * balance_now - cash_delta) / lotsize) * lotsize;

                                var price = trade_vol > 0 ? perpetual_contract.AskPrice : perpetual_contract.BidPrice;
                                price = Math.Floor(price / minprice) * minprice;


                                //下单模块，距离上次下单大于10秒 && 没有未成交挂单 && 订单匹配成功 && 没有正在处理的twap

                                if ((DateTime.Now - last_submit_order_time).TotalSeconds >= 59 && !om.HasOpenOrder() && om.CheckOrder() && pm.CheckPosition() && Math.Abs(trade_vol) >= lotsize && twap_tradable)//Math.Abs(trade_vol)>=1m/Convert.ToDecimal(Math.Pow(10,precision[symbol])))//brokerorder，localorder，openorder匹配
                                {
                                    delta_now = (decimal)target_delta;
                                    ////下单，将订单存入localorder
                                    //om.AddOrder(future_chain, perpetual_contract.Symbol, trade_vol, OrderType.Limit, price);
                                    ////将下单量存入仓位管理
                                    //pm.AddPosition(trade_vol);

                                    //Twap下单模块
                                    //生成新的twap订单类
                                    Twap twp = new Twap(algo, om, pm, future_chain, symbol, trade_vol, OrderType.Limit, 60, 5);
                                    //加入到twap订单管理数组
                                    TwapList.Add(twp);



                                    //更新本次下单时间
                                    last_submit_order_time = DateTime.Now;
                                    //发消息
                                    System.Diagnostics.Debug.WriteLine($"hold vol : " + holding_underlying_vol + " " + symbol);
                                    System.Diagnostics.Debug.WriteLine($"place order : " + trade_vol + " " + symbol);
                                    System.Diagnostics.Debug.WriteLine($"hold delta : " + Math.Round(cash_delta / balance_now, 2) + " " + symbol);
                                    System.Diagnostics.Debug.WriteLine($"target delta : " + Math.Round(target_delta, 2) + " " + symbol);
                                    System.Diagnostics.Debug.WriteLine($"size : " + Math.Round(balance_now, 4) + " " + symbol);
                                    System.Diagnostics.Debug.WriteLine($"----------------------------------------------------------------------------");
                                    OrderMessage odm = new OrderMessage();
                                    odm.SendMessage("Deribit_Delta hold vol : " + holding_underlying_vol + " " + symbol
                                        + System.Environment.NewLine +
                                        "Deribit_Delta place TWAP order : " + trade_vol + " " + symbol
                                        + System.Environment.NewLine +
                                        "Deribit_Delta hold delta : " + Math.Round(cash_delta / balance_now, 2) + " " + symbol
                                        + System.Environment.NewLine +
                                        "Deribit_Delta target delta : " + Math.Round(target_delta, 2) + " " + symbol
                                        + System.Environment.NewLine +
                                        "Deribit_Delta size: " + Math.Round(balance_now, 4) + " " + symbol
                                        , "MonteCarloDelta");
                                    //odm.SendMessage("Deribit_Delta place order : " + trade_vol + " " + symbol, "MonteCarloDelta");
                                    //odm.SendMessage("Deribit_Delta hold delta : " + Math.Round(cash_delta / balance_now, 2) + " " + symbol,"MonteCarloDelta");
                                    //odm.SendMessage("Deribit_Delta target delta : " + Math.Round(target_delta, 2) + " " + symbol, "MonteCarloDelta");
                                    //odm.SendMessage("Deribit_Delta size: " + Math.Round(balance_now, 4) + " " + symbol, "MonteCarloDelta");
                                    //odm.SendMessage("Deribit_Delta ----------------------------------------------------------------------------","MonteCarloDelta");
                                }
                                else
                                {
                                    //进行预警
                                    if ((DateTime.Now - last_warning_time).TotalSeconds >= 10)
                                    {
                                        OrderMessage odm = new OrderMessage();
                                        if (om.HasOpenOrder())
                                        {
                                            System.Diagnostics.Debug.WriteLine($"has open order !!!" + symbol);
                                            odm.SendMessage("Deribit_Delta -- has open order !!!" + symbol, "MonteCarloDelta");
                                        }

                                        if (!om.CheckOrder())
                                        {
                                            System.Diagnostics.Debug.WriteLine($"check order failed !!!" + symbol);
                                            odm.SendMessage("Deribit_Delta -- check order failed !!!" + symbol, "MonteCarloDelta");
                                        }

                                        if (!pm.CheckPosition())
                                        {
                                            System.Diagnostics.Debug.WriteLine($"check position failed !!!" + symbol);
                                            odm.SendMessage("Deribit_Delta -- check position failed !!!" + symbol, "MonteCarloDelta");
                                        }
                                        //更新本次预警时间
                                        last_warning_time = DateTime.Now;
                                        System.Diagnostics.Debug.WriteLine($"----------------------------------------------------------------------------");
                                        //odm.SendMessage("Deribit_Delta ----------------------------------------------------------------------------", "MonteCarloDelta");
                                    }

                                }
                            }

                        }
                    }
                    if (underlying_price > upline_now)
                    {
                        iscrossupline = true;

                    }
                    //突破上线
                    if (iscrossupline)
                    {
                        if (t > 0)
                        {
                            var putPrice = _priceModel.Evaluate(S, X, 0, r, sigma, expirydatenow, EPutCall.Put);
                            var target_delta_put = putPrice.Greeks.Delta;
                            //组合的detla,买1put，持有1现货
                            var target_delta = target_delta_put + 1;
                            var delta_up = (decimal)target_delta + 0.2m;
                            var delta_down = (decimal)target_delta - 0.2m;
                            decimal holding_underlying_vol = 0;
                            var holding_underlying = algo.Portfolio.Securities.Values.Where(
                                x =>
                                    x.HoldStock == true && x.Symbol.SecurityType == SecurityType.Future && x.Symbol.Value.Contains(symbol)
                            );
                            if (holding_underlying.Count() > 0)
                            {
                                holding_underlying_vol = holding_underlying.FirstOrDefault().Holdings.Quantity;
                            }

                            var cash_delta = holding_underlying_vol;
                            //超出对冲带，进行对冲
                            if (cash_delta > delta_up * balance_now || cash_delta < delta_down * balance_now)
                            {

                                //var trade_vol = Math.Round((decimal) target_delta * balance_now - cash_delta,precision[symbol]);
                                //var price = trade_vol > 0 ? perpetual_contract.AskPrice : perpetual_contract.BidPrice;

                                var lotsize = perpetual_contract.Symbol.SymbolProperties.LotSize;
                                var minprice = perpetual_contract.Symbol.SymbolProperties.MinimumPriceVariation;

                                var trade_vol =
                                    Math.Floor(((decimal)target_delta * balance_now - cash_delta) / lotsize) * lotsize;

                                var price = trade_vol > 0 ? perpetual_contract.AskPrice : perpetual_contract.BidPrice;
                                price = Math.Floor(price / minprice) * minprice;


                                //下单模块
                                if ((DateTime.Now - last_submit_order_time).TotalSeconds >= 59 && !om.HasOpenOrder() && om.CheckOrder() && pm.CheckPosition() && Math.Abs(trade_vol) >= lotsize && twap_tradable)//Math.Abs(trade_vol) >= 1m / Convert.ToDecimal(Math.Pow(10, precision[symbol])))
                                {
                                    delta_now = (decimal)target_delta;
                                    ////下单，将订单存入localorder
                                    //om.AddOrder(future_chain, perpetual_contract.Symbol, trade_vol, OrderType.Limit, price);
                                    //pm.AddPosition(trade_vol);

                                    //Twap下单模块
                                    //生成新的twap订单类
                                    Twap twp = new Twap(algo, om, pm, future_chain, symbol, trade_vol, OrderType.Limit, 60, 5);
                                    //加入到twap订单管理数组
                                    TwapList.Add(twp);


                                    last_submit_order_time = DateTime.Now;

                                    System.Diagnostics.Debug.WriteLine($"crossupline -- hold vol : " + holding_underlying_vol + " " + symbol);
                                    System.Diagnostics.Debug.WriteLine($"crossupline -- place order : " + trade_vol + " " + symbol);
                                    System.Diagnostics.Debug.WriteLine($"crossupline -- hold delta : " + Math.Round(cash_delta / balance_now, 2) + " " + symbol);
                                    System.Diagnostics.Debug.WriteLine($"crossupline -- target delta : " + Math.Round(target_delta, 2) + " " + symbol);
                                    System.Diagnostics.Debug.WriteLine($"size : " + Math.Round(balance_now, 4) + " " + symbol);
                                    System.Diagnostics.Debug.WriteLine($"----------------------------------------------------------------------------");
                                    OrderMessage odm = new OrderMessage();
                                    odm.SendMessage("MonteCarlo_Delta crossupline -- hold vol : " + holding_underlying_vol + " " + symbol
                                        + System.Environment.NewLine +
                                        "MonteCarlo_Delta crossupline -- place TWAP order : " + trade_vol + " " + symbol
                                        + System.Environment.NewLine +
                                        "MonteCarlo_Delta crossupline -- hold delta : " + Math.Round(cash_delta / balance_now, 2) + " " + symbol
                                        + System.Environment.NewLine +
                                        "MonteCarlo_Delta crossupline -- target delta : " + Math.Round(target_delta, 2) + " " + symbol
                                        + System.Environment.NewLine +
                                        "MonteCarlo_Delta crossupline -- size: " + Math.Round(balance_now, 4) + " " + symbol
                                        , "MonteCarloDelta");

                                    //odm.SendMessage("MonteCarlo_Delta crossupline -- hold vol : " + holding_underlying_vol + " " + symbol, "MonteCarloDelta");
                                    //odm.SendMessage("MonteCarlo_Delta crossupline -- place order : " + trade_vol + " " + symbol, "MonteCarloDelta");
                                    //odm.SendMessage("MonteCarlo_Delta crossupline -- hold delta : " + Math.Round(cash_delta / balance_now, 2) + " " + symbol, "MonteCarloDelta");
                                    //odm.SendMessage("MonteCarlo_Delta crossupline -- target delta : " + Math.Round(target_delta, 2) + " " + symbol, "MonteCarloDelta");
                                    //odm.SendMessage("MonteCarlo_Delta crossupline --  size: " + Math.Round(balance_now, 4) + " " + symbol, "MonteCarloDelta");
                                    //odm.SendMessage("MonteCarlo_Delta ----------------------------------------------------------------------------", "MonteCarloDelta");
                                }
                                else
                                {
                                    if ((DateTime.Now - last_warning_time).TotalSeconds >= 10)
                                    {
                                        OrderMessage odm = new OrderMessage();
                                        if (om.HasOpenOrder())
                                        {
                                            System.Diagnostics.Debug.WriteLine($"crossupline -- has open order !!!" + symbol);
                                            odm.SendMessage("MonteCarlo_Delta crossupline -- has open order !!!" + symbol, "MonteCarloDelta");
                                        }

                                        if (!om.CheckOrder())
                                        {
                                            System.Diagnostics.Debug.WriteLine($"crossupline -- check order failed !!!" + symbol);
                                            odm.SendMessage("MonteCarlo_Delta crossupline -- check order failed !!!" + symbol, "MonteCarloDelta");
                                        }

                                        if (!pm.CheckPosition())
                                        {
                                            System.Diagnostics.Debug.WriteLine($"crossupline -- check position failed !!!" + symbol);
                                            odm.SendMessage("MonteCarlo_Delta crossupline -- check position failed !!!" + symbol, "MonteCarloDelta");
                                        }

                                        last_warning_time = DateTime.Now;
                                        System.Diagnostics.Debug.WriteLine($"----------------------------------------------------------------------------");
                                        //odm.SendMessage("MonteCarlo_Delta ----------------------------------------------------------------------------", "MonteCarloDelta");
                                    }


                                }

                            }
                        }

                    }
                    //3秒撤单，剔除localorder，brokerorder
                    om.ManageOrder(future_chain, DateTime.UtcNow);
                    //查询当前持仓
                    pm.ManagePosition();
                    //stoplimit模块
                    //    var stop_limit_holding_up = algo.Transactions.GetOpenOrders().Where(
                    //        x => x.Type == OrderType.StopLimit && x.Symbol.Value.Contains(symbol) && x.Quantity > 0
                    //    );
                    //    var stop_limit_holding_down = algo.Transactions.GetOpenOrders().Where(
                    //        x => x.Type == OrderType.StopLimit && x.Symbol.Value.Contains(symbol) && x.Quantity < 0
                    //    );
                    //    //当前没有stoplimit挂单
                    //    if (stop_limit_holding_up.Count() == 0 || stop_limit_holding_down.Count() == 0)
                    //    {
                    //        stoplimit_upline = underlying_price * 1.05m;
                    //        stoplimit_lowline = underlying_price * 0.95m;
                    //        //挂stoplimit
                    //        BSModel bs = new BSModel();
                    //        if (!iscrossupline)
                    //        {
                    //            //call的delta
                    //            double delta_call_upline = bs.GetDelta(Convert.ToDouble(stoplimit_upline), X_call, 0, r, sigma, t, BSModel.EPutCall.Call);
                    //            //put的detla
                    //            var delta_put_upline = bs.GetDelta(Convert.ToDouble(stoplimit_upline), X, 0, r, sigma, t, BSModel.EPutCall.Put);

                    //            //组合的detla，卖2call，买1put，持有1现货
                    //            var target_delta_upline = -2 * delta_call_upline + delta_put_upline + 1;

                    //            //call的delta
                    //            double delta_call_lowline = bs.GetDelta(Convert.ToDouble(stoplimit_lowline), X_call, 0, r, sigma, t, BSModel.EPutCall.Call);
                    //            //put的detla
                    //            var delta_put_lowline = bs.GetDelta(Convert.ToDouble(stoplimit_lowline), X, 0, r, sigma, t, BSModel.EPutCall.Put);

                    //            //组合的detla，卖2call，买1put，持有1现货
                    //            var target_delta_lowline = -2 * delta_call_lowline + delta_put_lowline + 1;

                    //            //查询现货持仓
                    //            decimal holding_underlying_vol = 0;
                    //            var holding_underlying = algo.Portfolio.Securities.Values.Where(
                    //                x =>
                    //                    x.HoldStock == true && x.Symbol.SecurityType == SecurityType.Future && x.Symbol.Value.Contains(symbol)
                    //            );
                    //            if (holding_underlying.Count() > 0)
                    //            {
                    //                holding_underlying_vol = holding_underlying.FirstOrDefault().Holdings.Quantity;
                    //            }

                    //            var cash_delta = holding_underlying_vol;
                    //            algo.StopLimitOrder(
                    //                perpetual_contract.Symbol,
                    //                Math.Round(Convert.ToDecimal(target_delta_upline) - cash_delta,2),
                    //                Math.Round(stoplimit_upline,2),
                    //                Math.Round(stoplimit_upline * 1.005m,2)
                    //            );
                    //            algo.StopLimitOrder(
                    //                perpetual_contract.Symbol,
                    //                Math.Round(Convert.ToDecimal(target_delta_lowline) - cash_delta,2),
                    //                Math.Round(stoplimit_lowline,2),
                    //                Math.Round(stoplimit_lowline * 0.995m,2)
                    //            );

                    //        }

                    //        if (iscrossupline)
                    //        {

                    //            //put的detla
                    //            var delta_put_upline = bs.GetDelta(Convert.ToDouble(stoplimit_upline), X, 0, r, sigma, t, BSModel.EPutCall.Put);

                    //            //组合的detla，买1put，持有1现货
                    //            var target_delta_upline = delta_put_upline + 1;


                    //            //put的detla
                    //            var delta_put_lowline = bs.GetDelta(Convert.ToDouble(stoplimit_lowline), X, 0, r, sigma, t, BSModel.EPutCall.Put);

                    //            //组合的detla，买1put，持有1现货
                    //            var target_delta_lowline = delta_put_lowline + 1;

                    //            //查询现货持仓
                    //            decimal holding_underlying_vol = 0;
                    //            var holding_underlying = algo.Portfolio.Securities.Values.Where(
                    //                x =>
                    //                    x.HoldStock == true && x.Symbol.SecurityType == SecurityType.Future && x.Symbol.Value.Contains(symbol)
                    //            );
                    //            if (holding_underlying.Count() > 0)
                    //            {
                    //                holding_underlying_vol = holding_underlying.FirstOrDefault().Holdings.Quantity;
                    //            }

                    //            var cash_delta = holding_underlying_vol;
                    //            algo.StopLimitOrder(
                    //                perpetual_contract.Symbol,
                    //                Math.Round(Convert.ToDecimal(target_delta_upline) - cash_delta,2),
                    //                Math.Round(stoplimit_upline,2),
                    //                Math.Round(stoplimit_upline * 1.005m,2)
                    //            );
                    //            algo.StopLimitOrder(
                    //                perpetual_contract.Symbol,
                    //                Math.Round(Convert.ToDecimal(target_delta_lowline) - cash_delta,2),
                    //                Math.Round(stoplimit_lowline,2),
                    //                Math.Round(stoplimit_lowline * 0.995m,2)
                    //            );
                    //        }


                    //    }
                    //    else if (stop_limit_holding_up.Count() == 1 && stop_limit_holding_down.Count() == 1)
                    //    {
                    //        if (underlying_price > stoplimit_upline * 0.98m || underlying_price < stoplimit_lowline * 1.02m)
                    //        {
                    //            var stop_limit_holding = algo.Transactions.GetOpenOrders().Where(
                    //                x => x.Type == OrderType.StopLimit && x.Symbol.Value.Contains(symbol)
                    //            );
                    //            foreach (var order in stop_limit_holding)
                    //            {
                    //                var ticket = algo.Transactions.GetOrderTicket(order.Id);
                    //                var response = ticket.Cancel("Cancel Order");
                    //            }
                    //        }

                    //    }
                    //    else//stoplimit挂多了，或者撤单没撤掉
                    //    {
                    //        var stop_limit_holding = algo.Transactions.GetOpenOrders().Where(
                    //            x => x.Type == OrderType.StopLimit && x.Symbol.Value.Contains(symbol)
                    //        );
                    //        foreach (var order in stop_limit_holding)
                    //        {
                    //            var ticket = algo.Transactions.GetOrderTicket(order.Id);
                    //            var response = ticket.Cancel("Cancel Order");
                    //        }
                    //    }

                }
            }
        }

        /// <summary>
            /// Event - v3.0 DATA EVENT HANDLER: (Pattern) Basic template for user to override for receiving all subscription data in a single event
            /// </summary>
            /// <param name="slice">The current slice of data keyed by symbol string</param>
            public override void OnData(Slice slice)
        {
            //每次启动前缓存历史数据
            if (IsWarmingUp)
            {
                foreach (string symbol in _coinList)
                {
                    var lst = slice.Ticks.Where(x => x.Key.Value.Contains(symbol)).Select(x => x.Value);
                    var first = lst.First();
                    if (first != null)
                    {
                        var lstValue = first.First();
                        if (lstValue != null)
                        {
                            var p = lstValue.LastPrice;
                            if (slice.Time.Date < DateTime.Now.Date)
                            {
                                price_dict[symbol].Add(p);
                                //System.Diagnostics.Debug.WriteLine($"slice.Time:{slice.Time.ToString("yyyy-MM-dd hh:mm:ss")}" + symbol + " " + p);
                            }
                        }
                    }
                }

                return;
            }



            FuturesChain future_chain;
            //print的价格
            Dictionary<string, decimal> price_out_list = new Dictionary<string, decimal> { };

            //对币种进行循环，依次执行策略
            foreach (string symbol in _coinList)
            {
                if (slice.FutureChains.TryGetValue(_symbolDict[symbol], out future_chain))
                {


                    if (tradable)
                    {
                        //首次运行，获取实时价格，初始化订单管理、仓位管理模块
                        if (first_run_list[symbol])
                        {
                            underlying_price_list[symbol] = future_chain.Ticks.Values.FirstOrDefault().LastOrDefault().Price;
                            om_list[symbol] = new OrderManager(this, DateTime.Now, symbol);
                            pm_list[symbol] = new PositionManager(this, symbol);
                            first_run_list[symbol] = false;
                        }

                        var strategies_btc = AddOneStrategies.Where(x => x.symbol.Contains(symbol));
                        if (strategies_btc.Count() == 0)
                        {
                            AddOneStrategy aos = new AddOneStrategy(symbol, account_name, 0.1m);
                            AddOneStrategies.Add(aos);
                        }
                        //运行策略
                        for (int i = AddOneStrategies.Count() - 1; i >= 0; i--)
                        {
                            AddOneStrategy aos = AddOneStrategies[i];
                            if (aos.symbol == symbol)
                            {
                                aos.DetectionStrategy(slice, this, future_chain, om_list[symbol], pm_list[symbol]);
                            }
                        }

                        //未完成的twap，进行交易
                        TwapList.RemoveAll(x => x.isfinished);
                        foreach (Twap twp in TwapList)
                        {
                            FuturesContract perpetual_contract = null;
                            if (twp.symbol.Contains(symbol))
                            {
                                foreach (var item in future_chain)
                                {
                                    if (item.Symbol.Value.Contains(symbol))
                                    {
                                        perpetual_contract = item;
                                        //System.Diagnostics.Debug.WriteLine($"{item.Symbol.Value} {item.Time.ToString("yyyy-MM-dd hh:mm:ss")}");
                                        break;
                                    }
                                }
                            }

                            if (perpetual_contract != null)
                            {
                                twp.TwapTrade(slice, perpetual_contract);
                            }
                        }


                        //策略运行通知
                        if (slice.Time.TimeOfDay >= timeSpans && DateTime.Now > lastrunning_time_list[symbol].AddHours(23))
                        {

                            var last_underlying_close = future_chain.Ticks.Values.FirstOrDefault().LastOrDefault().Price;
                            if (last_underlying_close != 0)
                            {
                                lastrunning_time_list[symbol] = DateTime.Now;
                                OrderMessage odm = new OrderMessage();
                                string message = "MonteCarlo_Delta_Deribit_Alarm, Account " + account_name + " is running," + symbol + " price now is " + last_underlying_close.ToString();
                                odm.SendMessage(message, "MonteCarloDelta");
                            }

                        }
                        //每日0点存收盘价
                        if (slice.Time.TimeOfDay >= closeTime && DateTime.Now > lastclose_time_list[symbol].AddHours(23))
                        {

                            var last_underlying_close = future_chain.Ticks.Values.FirstOrDefault().LastOrDefault().Price;
                            if (last_underlying_close != 0)
                            {
                                lastclose_time_list[symbol] = DateTime.Now;
                                price_dict[symbol].Add(last_underlying_close);
                                OrderMessage odm = new OrderMessage();
                                string message = "MonteCarlo_Delta_Deribit_Alarm, Account " + account_name + " ADD CLOSE PRICE ," + symbol + " ,price now is " + last_underlying_close.ToString();
                                odm.SendMessage(message, "MonteCarloDelta");
                            }

                        }
                        //保证收盘价列表个数不超过15
                        if (price_dict[symbol].Count() > 15)
                        {
                            price_dict[symbol].RemoveAt(0);
                        }

                        //监控价格变动
                        if (future_chain.Ticks.Values.FirstOrDefault().LastOrDefault().Price / underlying_price_list[symbol] > 1.01m)
                        {
                            underlying_price_list[symbol] = future_chain.Ticks.Values.FirstOrDefault().LastOrDefault().Price;
                            OrderMessage odm = new OrderMessage();
                            string message = "MonteCarlo_Delta_Deribit " + symbol + " price increase 1%";
                            odm.SendMessage(message, "MonteCarloDelta");
                        }
                        price_out_list[symbol] = future_chain.Ticks.Values.FirstOrDefault().LastOrDefault().Price;
                    }
                }
            }


            //每两分钟序列化一次
            if (DateTime.Now > lastsaveinfotime.AddMinutes(2))
            {
                if (AddOneStrategies.Count > 0)
                {
                    var a = JsonConvert.SerializeObject(AddOneStrategies);
                    File.WriteAllText(jsonpath, a);
                }

                lastsaveinfotime = DateTime.Now;
                foreach (KeyValuePair<string, decimal> kvp in price_out_list)
                {
                    Log($"{account_name} ---" + kvp.Key + $" price is  { kvp.Value} ");
                }
            }
        }




        static decimal Var(List<decimal> v)
        {
            //    double tt = 2;
            //double mm = tt ^ 2;

            decimal sum1 = 0;
            for (int i = 0; i < v.Count; i++)
            {
                decimal temp = v[i] * v[i];
                sum1 = sum1 + temp;

            }

            decimal sum = 0;
            foreach (decimal d in v)
            {
                sum = sum + d;
            }

            decimal var = sum1 / v.Count - (sum / v.Count) * (sum / v.Count);
            return var;
        }
        /// <summary>
        /// Order fill event handler. On an order fill update the resulting information is passed to this method.
        /// </summary>
        /// <param name="orderEvent">Order event details containing details of the evemts</param>
        /// <remarks>This method can be called asynchronously and so should only be used by seasoned C# experts. Ensure you use proper locks on thread-unsafe objects</remarks>
        public override void OnOrderEvent(OrderEvent orderEvent)
        {
            System.Diagnostics.Debug.WriteLine("orderEvent" + orderEvent.ToString());
            OrderMessage odm = new OrderMessage();
            string message = "MonteCarlo_Delta_Deribit, account:  " + account_name + "-----" + orderEvent.ToString();
            //odm.SendMessage(message,"MonteCarloDelta");

            System.Diagnostics.Debug.WriteLine(orderEvent.ToString());

            Log(orderEvent.ToString());
            foreach (string symbol in _coinList)
            {


                if (orderEvent.Symbol.Value.Contains(symbol))
                {
                    var order = Transactions.GetOrderById(orderEvent.OrderId);
                    //考虑localorder未写入orderid，orderevent就触发的情况
                    if (!om_list[symbol].localOrders.ContainsKey(order.Id))
                    {
                        om_list[symbol].TempOrders[order.Id] = order;
                    }

                    //接收订单，将订单存入brokerorder，根据订单状态更新localorder
                    om_list[symbol].UpdateOrder(orderEvent);
                    //根据orderevent处理本地持仓
                    pm_list[symbol].UpdatePosition(orderEvent);
                }
            }

        }

        public override void OnEndOfAlgorithm()
        {
            //策略自动停止时，序列化
            if (AddOneStrategies.Count > 0)
            {
                var a = JsonConvert.SerializeObject(AddOneStrategies);
                File.WriteAllText(jsonpath, a);
            }
            OrderMessage odm = new OrderMessage();
            string message = "MonteCarlo_Delta_Deribit， account: " + account_name + "-----" + "Algrithm End";
            odm.SendMessage(message, "MonteCarloDelta");

        }


        public override void OnProgramExit()
        {
            //人工退出时，序列化
            if (AddOneStrategies.Count > 0)
            {
                var a = JsonConvert.SerializeObject(AddOneStrategies);
                File.WriteAllText(jsonpath, a);
            }
        }
        /// <summary>
        /// This is used by the regression test system to indicate if the open source Lean repository has the required data to run this algorithm.
        /// </summary>
        public bool CanRunLocally { get; } = true;

        /// <summary>
        /// This is used by the regression test system to indicate which languages this algorithm is written in.
        /// </summary>
        public Language[] Languages { get; } = { Language.CSharp, Language.Python };

        /// <summary>
        /// This is used by the regression test system to indicate what the expected statistics are from running the algorithm
        /// </summary>
        public Dictionary<string, string> ExpectedStatistics => new Dictionary<string, string>
        {
            {"Total Trades", "2"},
            {"Average Win", "0%"},
            {"Average Loss", "-0.28%"},
            {"Compounding Annual Return", "-78.282%"},
            {"Drawdown", "0.300%"},
            {"Expectancy", "-1"},
            {"Net Profit", "-0.282%"},
            {"Sharpe Ratio", "0"},
            {"Loss Rate", "100%"},
            {"Win Rate", "0%"},
            {"Profit-Loss Ratio", "0"},
            {"Alpha", "0"},
            {"Beta", "0"},
            {"Annual Standard Deviation", "0"},
            {"Annual Variance", "0"},
            {"Information Ratio", "0"},
            {"Tracking Error", "0"},
            {"Treynor Ratio", "0"},
            {"Total Fees", "$2.00"}
        };
    }
}
