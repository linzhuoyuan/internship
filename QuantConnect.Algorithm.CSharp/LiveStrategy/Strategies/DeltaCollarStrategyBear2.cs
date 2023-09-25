using QuantConnect.Algorithm.CSharp.LiveStrategy.DataType;
using BSModel = QuantConnect.Algorithm.CSharp.LiveStrategy.PricingModels.BSModel;
using QuantConnect.Algorithm.CSharp.qlnet.tools;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Fasterflect;
using QuantConnect.Securities;
using static QLNet.Callability;

namespace QuantConnect.Algorithm.CSharp.LiveStrategy.Strategies
{
    public class DeltaCollarStrategyBear2
    {
        //是否选出期权
        public bool HasOption {get; set;}

        //对冲开始时的持仓，对冲的手数
        public decimal BalanceNow { get; set; }

        //是否突破upline
        //public bool iscrossupline = false;

        //品种
        public Underlying Underlying { get; set;}

        //对冲开始时的价格，put的strike
        public decimal UnderlyingPriceNow {get; set;}

        //上线，call的strike
        public decimal UplineNow {get; set;}

        //到期日
        public DateTime ExpiryDateNow = new DateTime(2021, 01, 01, 16, 02, 0);
        public decimal TargetDelta {get; set;}
        public decimal CashDelta { get; set; }
        public string OutputBaseDirectory { get; set; }

        //理论的现货手数
        // public decimal TargetUnderlyingVolume {get; set;}
        public double Sigma {get;set;}
        public decimal HedgeRange { get; set; } = 0.2m;
        private double _deltaDecayMinutes = 30;
        private bool _tradeFlag = false;
        public bool IsDecay {get; private set;} = false;
        public decimal OldStrike { get; set; } = decimal.MaxValue;
        public Utils.MoveStrikeType StrategeType { get; set; } = Utils.MoveStrikeType.MoveUpCashGamma;
        public double GammaLimit { get; set; } = -1;
        public double Gamma { get; set; }
        public double T2M { get; set; }
        public double InitialT2M { get; set; } = 1;
        public double T2MUpdateRatio { get; set; }
        public int Index { get; set; }
        public decimal LimitPrice { get; set; }
        public decimal ExtremePrice { get; set; }
        public double MarketHours { get; set; } = 24;
        public bool FirstCrossLimitPrice { get; set; }

        //期权的列表
        public IList<OptionInPortfolio> Options = new List<OptionInPortfolio>();

        public DeltaCollarStrategyBear2(Underlying underlying)
        {
            Underlying = underlying;
        }

        public void UpdateExpiry(DateTime timeNow, double addHours, IEnumerable<MarketHoursSegment> marketHoursSegments,
            IList<DateTime> holidays, Dictionary<DateTime, TimeSpan> earlyCLoses)
        {
            ExpiryDateNow = timeNow;
            var iterator = marketHoursSegments.GetEnumerator();
            var start = false;
            while (addHours >= 0)
            {
                if (!iterator.MoveNext())
                {
                    iterator.Reset();
                    iterator.MoveNext();
                }

                var segment = iterator.Current;

                if (!start && !segment.Contains(ExpiryDateNow.TimeOfDay))
                {
                    continue;
                }

                if (start)
                {
                    var rollingDays = 0;
                    if (ExpiryDateNow.TimeOfDay >= segment.End)
                    {
                        if (ExpiryDateNow.DayOfWeek == DayOfWeek.Friday)
                        {
                            rollingDays = 3;
                        }
                        else
                        {
                            rollingDays = 1;
                        }
                    }
                    ExpiryDateNow = ExpiryDateNow.Date.AddDays(rollingDays).Add(segment.Start);

                    if (rollingDays == 0 && earlyCLoses.ContainsKey(ExpiryDateNow.Date))
                    {
                        continue;
                    }
                }

                start = true;

                if (segment.Contains(ExpiryDateNow.TimeOfDay))
                {
                    var leftHours = (segment.End - ExpiryDateNow.TimeOfDay).TotalHours;
                    if (leftHours > addHours)
                    {
                        ExpiryDateNow = ExpiryDateNow.AddHours(addHours);
                        break;
                    }

                    addHours -= leftHours;
                    ExpiryDateNow = ExpiryDateNow.AddHours(leftHours);
                }
            }
            iterator.Dispose();

            foreach (var day in holidays.Where(d =>
                         d.Date > timeNow.Date && d.Date.DayOfWeek != DayOfWeek.Saturday &&
                         d.Date.DayOfWeek != DayOfWeek.Sunday))
            {
                if (ExpiryDateNow.Date >= day.Date)
                {
                    ExpiryDateNow = ExpiryDateNow.AddDays(1);
                    if (ExpiryDateNow.DayOfWeek == DayOfWeek.Saturday)
                    {
                        ExpiryDateNow = ExpiryDateNow.AddDays(2);
                    }
                    else if (ExpiryDateNow.DayOfWeek == DayOfWeek.Sunday)
                    {
                        ExpiryDateNow = ExpiryDateNow.AddDays(1);
                    }
                }
            }
        }

        public IList<string> DetectionStrategy(DateTime sliceTime, decimal underlyingPrice, double sigma, decimal markPrice,
            decimal balanceNow = -9999, bool stopProfit = false, bool moveStrike = false, decimal updateStrikeRatio = 1m,
            IEnumerable<MarketHoursSegment> marketHourSegments = null, IList<DateTime> holidays = null, Dictionary<DateTime, TimeSpan> earlyCloses = null)
        {
            var response = new List<string>();
            UnderlyingPriceNow = underlyingPrice;
            ExtremePrice = Math.Min(ExtremePrice, underlyingPrice);
            var t2MUpdateRatio = T2MUpdateRatio;
            // if (FirstCrossLimitPrice && LimitPrice > ExtremePrice && updateStrikeRatio > 1)
            // {
            //     LimitPrice = LimitPrice * (2m - updateStrikeRatio);
            // }
            // else if (FirstCrossLimitPrice)
            // {
            //     FirstCrossLimitPrice = false;
            // }

            if (markPrice <= 0)
            {
                markPrice = underlyingPrice;
            }

            if (markPrice < LimitPrice && !FirstCrossLimitPrice)
            {
                //OldStrike = underlyingPrice * updateStrikeRatio;
                FirstCrossLimitPrice = true;
                //Options[0].Strike = OldStrike;
            }

            // if (FirstCrossLimitPrice)
            // {
            //     OldStrike = Math.Min(OldStrike, markPrice * updateStrikeRatio);
            // }

            //当第一次运行或到期日，更新持仓，及本周期对冲期权手数
            if (sliceTime >= ExpiryDateNow)
            {
                HasOption = false;
                IsDecay = false;
                if (balanceNow != -9999)
                {
                    BalanceNow = balanceNow;
                }
                if (markPrice > LimitPrice && FirstCrossLimitPrice)
                {
                    // while (underlyingPrice * 0.9m < OldStrike && updateStrikeRatio > 1)
                    // {
                    //     OldStrike = OldStrike * 0.9m;
                    // }
                    // //OldStrike = Math.Min(OldStrike, LimitPrice);
                    // //Options[0].Strike = OldStrike;
                    
                    // t2MUpdateRatio = 0;
                    // if (T2M > 0)
                    // {
                    //     T2M = InitialT2M;
                    // }
                    LimitPrice = ExtremePrice * (1 - 0.01m * Index);
                    OldStrike = Math.Min(OldStrike, LimitPrice);
                }
            }

            //选择对冲的期权属性
            if (!HasOption)
            {
                Options = new List<OptionInPortfolio>();
                HasOption = true;
                response.Add($"{Underlying.CoinPair}到期换月, current T2M is {T2M}");
                var addHours = T2M;
                var price = underlyingPrice * updateStrikeRatio;
                //if (underlyingPrice < LimitPrice)
                //{
                    if (T2M < 10 * MarketHours)
                    {
                        addHours += 0.1 * t2MUpdateRatio * T2M * Index;
                        T2M += t2MUpdateRatio * T2M;
                        if (T2M > 10 * MarketHours)
                        {
                            T2M = 10 * MarketHours;
                            var oldT2M = T2M / t2MUpdateRatio;
                            var ratio = 10 * MarketHours / oldT2M;
                            addHours = oldT2M + 0.1 * ratio * oldT2M * Index;
                        }
                    }
                //}
                /*else
                {
                    T2M = InitialT2M;
                    addHours = InitialT2M;
                    price = ExtremePrice;
                }*/

                if (marketHourSegments is not null)
                {
                    UpdateExpiry(ExpiryDateNow, addHours, marketHourSegments, holidays, earlyCloses);
                }
                else
                {
                    ExpiryDateNow = ExpiryDateNow.AddHours(addHours);
                }

                OldStrike = Math.Min(OldStrike, price);
                Options.Add(
                    new OptionInPortfolio("put", stopProfit ? OldStrike : price, 1, false, false));

                // if((decimal)put_price/UnderlyingPriceNow>0.08m){
                //     var strike_put_ratio = 1.05m;
                //     //2倍call的价格>=平值put的价格
                //     for (decimal i = 1.20m; i >= 1.05m; i -= 0.01m)
                //     {
                //         var call_price = bs.GetOptionValue((double)UnderlyingPriceNow, (double)(UnderlyingPriceNow * i), 0, 0.07, sigma, t, BSModel.EPutCall.Call);
                //         if (2 * call_price > put_price)
                //         {
                //             strike_put_ratio = i;
                //             break;
                //         }
                //     }
                //     //根据定价确定upline
                //     UplineNow = UnderlyingPriceNow * strike_put_ratio;
                // }


                //持有的现货手数1
                //TargetUnderlyingVolume = 0;


            }

            //建仓完成后
            if (HasOption)
            {
                //当前标的价格
                double S = Convert.ToDouble(underlyingPrice);

                //利率
                double r = 0.07;

                /*double sigma = 0;
                if (priceList.Count() >= 15)
                {
                    //根据历史数据计算波动率
                    List<decimal> ret_list = new List<decimal> { };
                    for (int i = 1; i < priceList.Count; i++)
                    {
                        ret_list.Add(
                            Convert.ToDecimal(Math.Log(Convert.ToDouble(priceList[i] / priceList[i - 1]), Math.E))
                        );
                    }
                    ret_list = ret_list.Skip(ret_list.Count()-15).Take(15).ToList();
                    sigma = Math.Sqrt(Convert.ToDouble(Utils.Var(ret_list))) * Math.Sqrt(365);
                }

                if (sigma <= 1e-15)
                {
                    sigma = 0.7;
                }*/
                //距离到期日
                var t = (ExpiryDateNow - sliceTime).TotalDays / 365.0;
                if (t < 0)
                {
                    throw new Exception("Expired synthetic option!!!!!!");
                }

                var t2MMinutes = (ExpiryDateNow - sliceTime).TotalMinutes;

                if (false) //t2MMinutees <= _deltaDecayMinutes)
                {
                    IsDecay = true;
                    var decayTarget = stopProfit ? 0 : 0.5;
                    CashDelta =
                        (decimal) (decayTarget - (double) TargetDelta * (_deltaDecayMinutes - t2MMinutes) /
                            _deltaDecayMinutes) + TargetDelta;
                }
                else if (t > 0)
                {
                    //计算所有期权持仓的delta
                    double delta_total = 0;
                    foreach (var op in Options)
                    {
                        if (MoveStrike(underlyingPrice, op, sigma, t))
                        {
                            if (!string.IsNullOrWhiteSpace(OutputBaseDirectory))
                            {
                                File.AppendAllText($"{OutputBaseDirectory}/{Underlying.CoinPair}-movestrike.csv",
                                    $"{ExpiryDateNow},{sliceTime},{op.Strike},{underlyingPrice}\n");
                            }

                            op.Strike = underlyingPrice;
                            OldStrike = underlyingPrice;
                            if ((ExpiryDateNow.Date - sliceTime.Date).TotalDays < 10)
                            {
                                ExpiryDateNow = ExpiryDateNow.AddDays(10);
                            }
                        }

                        delta_total +=
                            BSModel.GetDelta(S, (double) op.Strike, 0, r, sigma, t, BSModel.EPutCall.Put) *
                            (double) op.Volume;
                    }

                    //期权delta总和+理论现货
                    // TODO: BS model only provide double precision!!!
                    try
                    {
                        var decimalDelta = (decimal) delta_total;
                    }
                    catch (Exception e)
                    {
                        response.Add($"WRONG DELTA!!! S: {S}, sigma: {sigma}, t2M {t}");
                        response.Add(e.Message + "\n\n" + e.StackTrace);
                        return response;
                    }

                    TargetDelta = (decimal) delta_total; // + TargetUnderlyingVolume;
                    Sigma = sigma;
                    //由于优先交易现货，target delta不能为负
                    if (TargetDelta > 0)
                    {
                        TargetDelta = 0;
                        response.Add("DELTA ERROR: target delta > 0!!!!");
                    }

                    var delta_up = TargetDelta + HedgeRange;
                    var delta_down = TargetDelta - HedgeRange;
                    if (CashDelta > delta_up || CashDelta < delta_down)
                    {
                        CashDelta = TargetDelta;
                        _tradeFlag = true;
                    }

                    if (TargetDelta > -0.1m)
                    {
                        if (TargetDelta < 0 || !moveStrike)
                        {
                            _tradeFlag = CashDelta != 0;
                            CashDelta = 0;
                            /*if (TargetDelta == 0m && (OldStrike > LimitPrice || UnderlyingPriceNow > LimitPrice) &&
                                OldStrike > ExtremePrice)
                            {
                                Options[0].Strike = ExtremePrice;
                                OldStrike = ExtremePrice;
                                var op = Options.First();
                                CashDelta = (decimal) BSModel.GetDelta(S, (double) op.Strike, 0, r, sigma, t,
                                    BSModel.EPutCall.Put) * op.Volume;
                            }*/
                        }
                        else
                        {
                            _tradeFlag = true;
                            response.Add($"Move strike: {Underlying.CoinPair} {OldStrike} => {underlyingPrice}");
                            OldStrike = underlyingPrice;
                            Options[0].Strike = OldStrike;
                            var op = Options.First();
                            CashDelta = (decimal) BSModel.GetDelta(S, (double) op.Strike, 0, r, sigma, t,
                                BSModel.EPutCall.Put) * op.Volume;
                        }
                    }
                    else if (TargetDelta < -0.9m)
                    {
                        _tradeFlag = CashDelta != -1;
                        CashDelta = -1;
                        if (BalanceNow != 0 && balanceNow / BalanceNow > 1.2m ||
                            balanceNow != 0 && BalanceNow / balanceNow > 1.2m)
                        {
                            BalanceNow = balanceNow;
                        }
                    }

                    if (_tradeFlag && balanceNow >= 0)
                    {
                        BalanceNow = balanceNow;
                    }
                }
            }

            return response;
        }

        private bool MoveStrike(decimal underlyingPrice, OptionInPortfolio option, double sigma, double t2M)
        {
            if ((int)StrategeType > 0 && underlyingPrice <= option.Strike ||
                (int)StrategeType < 0 && underlyingPrice >= option.Strike || GammaLimit < 0)
            {
                return false;
            }

            /*if (IsPriceSurge)
            {
                if (underlyingPrice > option.Strike * 1.2m)
                {
                    return true;
                }
            }*/
            //var gamma = BSModel.GetGamma((double) underlyingPrice, (double) option.Strike, 0, 0.07, sigma, t2M);

            var gamma = BSModel.GetGamma((double)underlyingPrice, (double)option.Strike, 0, 0.07, sigma, t2M);
            Gamma = gamma * (double)underlyingPrice;
            return Gamma < GammaLimit;
        }

        public bool NeedHedge()
        {
            if (_tradeFlag)
            {
                _tradeFlag = false;
                return true;
            }

            return false;
        }
    }
}
