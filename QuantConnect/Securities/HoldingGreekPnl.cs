using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using QLNet;
using QuantConnect.Data;
using QuantConnect.Data.Market;
using QuantConnect.Securities.Option;

namespace QuantConnect.Securities
{
    class HoldingGreekPnl
    {
        public HoldingGreekPnl(SecurityHolding holding, Security security)
        {
            _holding = holding;
  //          _contract = contract;
            _security = security;
            _underlying = security.Symbol.Underlying;
            PnlData = new GreekPnlData();
            _PriceModel = OptionPriceModels.BlackScholes();
        }

        /// 上一个计算结果
        private OptionPriceModelResult _lastPriceModeResult;

        private IOptionPriceModel _PriceModel;
        private SecurityHolding _holding;
        private OptionContract _contract;
        private Security _security;
        private Symbol _underlying;

        /// 上一个标的的价格
        private Decimal _underlyingPrice;
        private Decimal _delta_s;
        private Decimal _delta_t;
        private Decimal _delta_r;

        private Decimal _price;

        /// 上一个计算时间
        private DateTime _tmLast;

        /// 上一个无风险利率
        private Decimal _rfRate;


        public GreekPnlData PnlData;

        private OptionPriceModelResult GetPriceModeResult(Slice slice)
        {
            return _PriceModel.Evaluate(_security, slice, _contract);
        }


        public bool CalcPnl(Slice slice)
        {
            if (_underlying == null)
            {
                if (_security.Symbol.SecurityType == SecurityType.Future ||_security.Symbol.SecurityType == SecurityType.Equity || _security.Symbol.SecurityType == SecurityType.Crypto )
                {
                    if (_underlyingPrice == 0)
                    {
                        _underlyingPrice = _security.Price;
                        return false;
                    }

                    var pnl = (_security.Price - _underlyingPrice) * _holding.Quantity;
                    PnlData.DeltaPnl += pnl;
                    PnlData.TotalPnl += pnl;
                    return true;
                }

                return false;
            }
            else
            {
                decimal minIv = Convert.ToDecimal(1e-2);
                foreach (var chain in slice.OptionChains.Values)
                {
                    var contract = chain.Where(x => x.Symbol == _security.Symbol).FirstOrDefault();
                    if (contract == null)
                        continue;

                    _contract = contract;
                    var lastPrice = _contract.LastPrice == 0
                        ? ( (_contract.AskPrice + _contract.BidPrice) / 2)
                        : _contract.LastPrice;

                    var result = GetPriceModeResult(slice);

                    if (_lastPriceModeResult == null)
                    {
                        _underlyingPrice = chain.Underlying.Price;
                        _tmLast = slice.Time;
                        _lastPriceModeResult = result;
                        _price = lastPrice;
                        break;
                    }

                    if (result.ImpliedVolatility < minIv || _lastPriceModeResult.ImpliedVolatility < minIv)
                    {
                        Logging.Log.Trace($"no ImV at {slice.Time}, the pnl of pnl_analysis will be calculated into no_imv_pnl");

                        var pnl = (lastPrice - _price) * _holding.Quantity;
                        PnlData.NoImvPnl += pnl;
                        PnlData.TotalPnl += pnl;

                        break;
                    }

                    _delta_s = this._underlyingPrice == 0 ? 0 :(chain.Underlying.Price - _underlyingPrice)/* /_underlyingPrice*/ ;

                    TimeSpan ts  = (_tmLast == DateTime.MinValue) ? TimeSpan.Zero : slice.Time -_tmLast ;
                    _delta_t = Convert.ToDecimal((ts.TotalSeconds / 60 / 60 / 24) / 365);

                    Greeks greeks = _lastPriceModeResult.Greeks;
                    decimal quantity = _holding.Quantity;

                    PnlData.DeltaPnl += greeks.Delta * _delta_s * quantity;
                    PnlData.GammaPnl += greeks.Gamma * Convert.ToDecimal(Math.Pow(Convert.ToDouble(_delta_s), 2.0)) / 2 * quantity;
                    PnlData.VegaPnl += greeks.Vega * (result.ImpliedVolatility - _lastPriceModeResult.ImpliedVolatility) * quantity;
                    PnlData.ThetaPnl += greeks.Theta * _delta_t * quantity;
                    PnlData.RhoPnl += greeks.Rho * _delta_r * quantity;

                    decimal total = PnlData.DeltaPnl + PnlData.GammaPnl + PnlData.VegaPnl + PnlData.ThetaPnl +
                                    PnlData.RhoPnl;
                    PnlData.TotalPnl += (lastPrice - _price) * quantity;

                    _underlyingPrice = chain.Underlying.Price;
                    _tmLast = slice.Time;
                    _lastPriceModeResult = result;
                    _price = lastPrice;
                    
                    return true;
                }

            }

            return false;
        }
   
    }
}
