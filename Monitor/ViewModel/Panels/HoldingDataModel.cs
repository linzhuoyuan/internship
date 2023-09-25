using GalaSoft.MvvmLight;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Monitor.ViewModel.Panels
{
    public class HoldingDataModel : ObservableObject
    {
        private Symbol _symbol;
        public Symbol Symbol
        {
            get => _symbol;
            set
            {
                _symbol = value;
                if (_symbol != null)
                {
                    code = _symbol.Value;
                }
                //RaisePropertyChanged();
            }
        }

        private string code;
        public string Code
        {
            get => code;
            set
            {
                code = value;
                if (Symbol != null)
                {
                    code = Symbol.Value;
                }
                RaisePropertyChanged(() => Code);
            }
        }

        private string type;
        public string Type
        {
            get => type;
            set
            {
                type = value;
                RaisePropertyChanged(() => Type);
            }
        }

        private string holdingType;
        public string HoldingType
        {
            get => holdingType;
            set
            {
                holdingType = value;
                RaisePropertyChanged(() => HoldingType);
            }
        }

        private string currencySymbol;
        public string CurrencySymbol
        {
            get => currencySymbol;
            set
            {
                currencySymbol = value;
                RaisePropertyChanged(() => CurrencySymbol);
            }
        }

        private decimal averagePrice;
        public decimal AveragePrice
        {
            get => averagePrice;
            set
            {
                averagePrice = value;
                RaisePropertyChanged(() => AveragePrice);
            }
        }

        private decimal quantity;
        public decimal Quantity
        {
            get => quantity;
            set
            {
                quantity = value;
                RaisePropertyChanged(() => Quantity);
            }
        }

        private decimal marketPrice;
        public decimal MarketPrice
        {
            get => marketPrice;
            set
            {
                marketPrice = value;
                RaisePropertyChanged(() => MarketPrice);
            }
        }

        private string conversionRate;
        public string ConversionRate
        {
            get => conversionRate;
            set
            {
                conversionRate = value;
                RaisePropertyChanged(() => ConversionRate);
            }
        }

        private decimal marketValue;
        public decimal MarketValue
        {
            get => marketValue;
            set
            {
                marketValue = value;
                RaisePropertyChanged(() => MarketValue);
            }
        }

        private string unrealizedPnL;
        public string UnrealizedPnL
        {
            get => unrealizedPnL;
            set
            {
                unrealizedPnL = value;
                RaisePropertyChanged(() => UnrealizedPnL);
            }
        }

        private string realizedPnL;
        public string RealizedPnL
        {
            get => realizedPnL;
            set
            {
                realizedPnL = value;
                RaisePropertyChanged(() => RealizedPnL);
            }
        }

        private string exercisePnL;
        public string ExercisePnL
        {
            get => exercisePnL;
            set
            {
                exercisePnL = value;
                RaisePropertyChanged(() => ExercisePnL);
            }
        }

        private DateTime time;
        public DateTime Time
        {
            get => time;
            set
            {
                time = value;
                RaisePropertyChanged(() => Time);
            }
        }
    }

    public class Symbol : ObservableObject
    {
        public string ID { get; set; }
        private string _value { get; set; }
        public string Value
        {
            get => _value;
            set
            {
                _value = value;
                RaisePropertyChanged();
            }
        }
        public string Permtick { get; set; }
    }
}
