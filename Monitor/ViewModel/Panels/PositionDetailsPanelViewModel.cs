using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Messaging;
using LiveCharts.Helpers;
using Monitor.Model;
using Monitor.Model.Messages;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace Monitor.ViewModel.Panels
{
    public class PositionDetailsPanelViewModel : ToolPaneViewModel
    {
        private readonly IMessenger _messenger;
        private ObservableCollection<HoldingDataModel> holdingDatas;
        public ObservableCollection<HoldingDataModel> HoldingDatas
        {
            get { return holdingDatas; }
            set
            {
                holdingDatas = value;
                RaisePropertyChanged(() => HoldingDatas);
            }
        }

        public PositionDetailsPanelViewModel()
        { }

        public PositionDetailsPanelViewModel(IMessenger messenger)
        {
            if (HoldingDatas == null)
                HoldingDatas = new ObservableCollection<HoldingDataModel>();

            Name = "持仓明细";
            _messenger = messenger;
            _messenger.Register<string>(this, "Holding", message =>
             {
                 try
                 {
                     List<HoldingDataModel> holdings = JsonConvert.DeserializeObject<List<HoldingDataModel>>(message);
                     //ViewHoldingData(holdings);
                     SetHoldingData(holdings);
                 }
                 catch { throw; }
             });
        }

        private void ViewHoldingData(List<HoldingDataModel> holdings)
        {
            if (holdings != null && holdings.Count > 0)
            {
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    if (HoldingDatas.Count > 0)
                    {
                        holdingDatas.Clear();
                    }
                });

                holdings.ForEach(holding =>
                {
                    HoldingDataModel hdm = new HoldingDataModel()
                    {
                        Code = holding.Code,
                        Type = holding.Type,
                        HoldingType = holding.HoldingType,
                        CurrencySymbol = holding.CurrencySymbol,
                        AveragePrice = holding.AveragePrice,
                        Quantity = holding.Quantity,
                        MarketPrice = holding.MarketPrice,
                        MarketValue = holding.MarketValue,
                        UnrealizedPnL = holding.UnrealizedPnL,
                        RealizedPnL = holding.RealizedPnL,
                        ExercisePnL = holding.ExercisePnL,
                        Time = holding.Time
                    };

                    if (holding.Code.StartsWith("BTC", StringComparison.CurrentCultureIgnoreCase))
                    {
                        hdm.CurrencySymbol = "฿";
                    }
                    if (holding.Code.StartsWith("TOTAL", StringComparison.CurrentCultureIgnoreCase))
                    {
                        hdm.Time = DateTime.Now;
                    }
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        HoldingDatas.Add(hdm);
                    });
                });
            }
        }

        List<string> codes = new List<string>();

        private void SetHoldingData(List<HoldingDataModel> holdings)
        {
            if (holdings != null && holdings.Count > 0)
            {
                if (HoldingDatas.Count > 0)
                {
                    foreach (var data in HoldingDatas)
                    {
                        if (!holdings.Exists(c => c.Code.Equals(data.Code)))
                        {
                            codes.Add(data.Code);
                        }
                    }
                    codes.Add("TOTAL");
                    if (codes.Count > 0)
                    {
                        codes.ForEach(c =>
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                HoldingDatas.Remove(HoldingDatas.FirstOrDefault(h => h.Code.Equals(c)));
                            });
                        });
                        codes.Clear();
                    }
                }

                holdings.ForEach(holding =>
                {
                    if (HoldingDatas.FirstOrDefault(h => h.Code.Equals(holding.Code)) != null)
                    {
                        UpdateHoldings(holding);
                    }
                    else
                    {
                        AddHolding(holding);
                    }
                });

                HoldingDatas.OrderBy(h => h.Code);
            }
        }

        private void UpdateHoldings(HoldingDataModel holding)
        {
            for (int i = 0; i < HoldingDatas.Count; i++)
            {
                if (HoldingDatas[i].Code == holding.Code)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        HoldingDatas[i] = holding;
                    });
                }
            }
        }

        private void AddHolding(HoldingDataModel holding)
        {
            if (holding.Code.StartsWith("BTC", StringComparison.CurrentCultureIgnoreCase))
            {
                holding.CurrencySymbol = "฿";
            }
            if (holding.Code.StartsWith("TOTAL", StringComparison.CurrentCultureIgnoreCase))
            {
                holding.Time = DateTime.Now;
                holding.HoldingType = string.Empty;
            }
            Application.Current.Dispatcher.Invoke(() =>
            {
                HoldingDatas.Add(holding);
            });
        }
    }
}
