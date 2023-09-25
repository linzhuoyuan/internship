using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Messaging;
using Newtonsoft.Json;
using RestSharp.Extensions;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace Monitor.ViewModel.Panels
{
    public class PerformanceAttributionPanelViewModel : ToolPaneViewModel
    {
        public PerformanceAttributionPanelViewModel() { }

        private ObservableCollection<HoldingPnlModel> holdingPnls;
        public ObservableCollection<HoldingPnlModel> HoldingPnls
        {
            get { return holdingPnls; }
            set
            {
                holdingPnls = value;
                RaisePropertyChanged(() => HoldingPnls);
            }
        }


        IMessenger messenger;

        public PerformanceAttributionPanelViewModel(IMessenger msager)
        {
            if (HoldingPnls == null)
            {
                HoldingPnls = new ObservableCollection<HoldingPnlModel>();
            }

            Name = "绩效归因";
            IsVisible = true;

            messenger = msager;
            messenger.Register<string>(this, "pnl", data =>
            {
                SetPnlData(data);
            });
        }

        private void SetPnlData(string data)
        {
            if (!string.IsNullOrEmpty(data))
            {
                var pnlData = JsonConvert.DeserializeObject<HoldingPnlData>(data);
                if (pnlData != null && pnlData.HoldingsPnl != null && pnlData.HoldingsPnl.Count > 0)
                {
                    Application.Current?.Dispatcher.Invoke(() =>
                    {
                        HoldingPnls.Clear();
                    });

                    foreach (var pnl in pnlData.HoldingsPnl)
                    {
                        pnl.DateTime = pnlData.DateTime;
                        Application.Current?.Dispatcher.Invoke(() =>
                        {
                            HoldingPnls.Add(pnl);
                        });
                    }

                    HoldingPnlModel totalPnl = new HoldingPnlModel();
                    totalPnl.holdingSymbol = "Total";
                    totalPnl.DeltaPnl = pnlData.HoldingsPnl.Sum(pnl=>pnl.DeltaPnl);
                    totalPnl.GammaPnl = pnlData.HoldingsPnl.Sum(pnl=>pnl.GammaPnl);
                    totalPnl.RhoPnl = pnlData.HoldingsPnl.Sum(pnl=>pnl.RhoPnl);
                    totalPnl.ThetaPnl = pnlData.HoldingsPnl.Sum(pnl=>pnl.ThetaPnl);
                    totalPnl.NoImvPnl = pnlData.HoldingsPnl.Sum(pnl=>pnl.NoImvPnl);
                    totalPnl.VegaPnl = pnlData.HoldingsPnl.Sum(pnl=>pnl.VegaPnl);                
                    totalPnl.TotalPnl = pnlData.HoldingsPnl.Sum(pnl=>pnl.TotalPnl);

                    Application.Current?.Dispatcher.Invoke(() =>
                    {
                        HoldingPnls.Add(totalPnl);
                    });
                }
            }
        }
    }

    public class HoldingPnlData
    {
        public DateTime DateTime { get; set; }
        public List<HoldingPnlModel> HoldingsPnl { get; set; }
    }

    public class HoldingPnlModel : ObservableObject
    {
        private string _holdingSymbol;
        public string holdingSymbol
        {
            get => _holdingSymbol;
            set
            {
                _holdingSymbol = value;
                RaisePropertyChanged();
            }
        }

        private string _holdingType;
        public string holdingType
        {
            get => _holdingType;
            set
            {
                _holdingType = value;
                RaisePropertyChanged(() => holdingType);
            }
        }

        private decimal deltaPnl;
        public decimal DeltaPnl
        {
            get => deltaPnl;
            set
            {
                deltaPnl = value;
                RaisePropertyChanged();
            }
        }

        private decimal gammaPnl;
        public decimal GammaPnl
        {
            get => gammaPnl;
            set
            {
                gammaPnl = value;
                RaisePropertyChanged();
            }
        }

        private decimal vegaPnl;
        public decimal VegaPnl
        {
            get => vegaPnl;
            set
            {
                vegaPnl = value;
                RaisePropertyChanged();
            }
        }

        private decimal thetaPnl;
        public decimal ThetaPnl
        {
            get => thetaPnl;
            set
            {
                thetaPnl = value;
                RaisePropertyChanged();
            }
        }

        private decimal rhoPnl;
        public decimal RhoPnl
        {
            get => rhoPnl;
            set
            {
                rhoPnl = value;
                RaisePropertyChanged();
            }
        }

        private decimal totalPnl;
        public decimal TotalPnl
        {
            get => totalPnl;
            set
            {
                totalPnl = value;
                RaisePropertyChanged();
            }
        }

        private decimal noImvPnl;
        public decimal NoImvPnl
        {
            get => noImvPnl;
            set
            {
                noImvPnl = value;
                RaisePropertyChanged();
            }
        }

        private DateTime dateTime;
        public DateTime DateTime
        {
            get => dateTime;
            set
            {
                dateTime = value;
                RaisePropertyChanged();
            }
        }

    }
}
