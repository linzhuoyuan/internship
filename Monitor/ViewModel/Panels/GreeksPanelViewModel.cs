using GalaSoft.MvvmLight.Messaging;
using LiveCharts;
using LiveCharts.Geared;
using LiveCharts.Helpers;
using QuantConnect;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Monitor.ViewModel.Panels
{
    public class GreeksPanelViewModel : ToolPaneViewModel
    {
        #region Check Visibilitys
        private bool deltaVisibility;
        private bool gammaVisibility;
        private bool vegaVisibility;
        private bool thetaVisibility;
        private bool rhoVisibility;

        public bool DeltaVisibility
        {
            get => deltaVisibility;
            set
            {
                deltaVisibility = value;
                RaisePropertyChanged("DeltaVisibility");
            }
        }

        public bool GammaVisibility
        {
            get => gammaVisibility;
            set
            {
                gammaVisibility = value;
                RaisePropertyChanged("GammaVisibility");
            }
        }

        public bool VegaVisibility
        {
            get => vegaVisibility;
            set
            {
                vegaVisibility = value;
                RaisePropertyChanged("VegaVisibility");
            }
        }

        public bool ThetaVisibility
        {
            get => thetaVisibility;
            set
            {
                thetaVisibility = value;
                RaisePropertyChanged("ThetaVisibility");
            }
        }
        public bool RhoVisibility
        {
            get => rhoVisibility;
            set
            {
                rhoVisibility = value;
                RaisePropertyChanged("RhoVisibility");
            }
        }

        #endregion
        #region Properties       

        private DateTime initialDateTime;
        private IAxisWindow selectedWin;
        private PeriodUnits period = PeriodUnits.Minutes;

        public DateTime InitialDateTime
        {
            get => initialDateTime;
            set
            {
                initialDateTime = value;
                RaisePropertyChanged(() => InitialDateTime);
            }
        }

        public PeriodUnits Period
        {
            get => period;
            set
            {
                period = value;
                RaisePropertyChanged(() => Period);
            }
        }

        public IAxisWindow SelectedWindow
        {
            get => selectedWin;
            set
            {
                selectedWin = value;
                RaisePropertyChanged(() => SelectedWindow);
            }
        }

        #region Series Values

        private GearedValues<double> deltaValues;
        public GearedValues<double> DeltaValues
        {
            get => deltaValues;
            set
            {
                deltaValues = value;
                RaisePropertyChanged(() => DeltaValues);
            }
        }

        private GearedValues<double> gammaValues;
        public GearedValues<double> GammaValues
        {
            get => gammaValues;
            set
            {
                gammaValues = value;
                RaisePropertyChanged(() => GammaValues);
            }
        }
        private GearedValues<double> vegaValues;
        public GearedValues<double> VegaValues
        {
            get => vegaValues;
            set
            {
                vegaValues = value;
                RaisePropertyChanged(() => VegaValues);
            }
        }

        private GearedValues<double> thetaValues;
        public GearedValues<double> ThetaValues
        {
            get => thetaValues;
            set
            {
                thetaValues = value;
                RaisePropertyChanged(() => ThetaValues);
            }
        }

        private GearedValues<double> rhoValues;
        public GearedValues<double> RhoValues
        {
            get => rhoValues;
            set
            {
                rhoValues = value;
                RaisePropertyChanged(() => RhoValues);
            }
        }
        #endregion

        #endregion
        private bool _once = false;
        public GreeksPanelViewModel() { }

        private readonly IMessenger _messenger;
        public GreeksPanelViewModel(IMessenger messenger)
        {
            YFormatter = value => value.ToString("f2");

            DeltaVisibility = true;
            GammaVisibility = true;
            Name = "Greeks";
            IsVisible = false;
            _messenger = messenger;
            _messenger.Register<List<GreeksChartData>>(this, "greeks", GreekData =>
            {
                if (GreekData != null && GreekData.Count > 0)
                {
                    if (!_once)
                    {
                        try
                        {
                            InitialValues(GreekData[0].DataTime);
                        }
                        catch
                        {
                            // ignored
                        }

                        IsVisible = true;
                        _once = true;
                    }

                    ViewGreeks(GreekData);
                }
                else { }
            });
        }

        public Func<double, string> YFormatter { get; set; }

        private void ViewGreeks(List<GreeksChartData> greeks)
        {
            greeks.ForEach(AddGreekData);
        }

        private void AddGreekData(GreeksChartData greek)
        {
            if (greek != null)
            {
                DeltaValues.Add(Convert.ToDouble(greek.Delta));
                GammaValues.Add(Convert.ToDouble(greek.Gamma));
                VegaValues.Add(Convert.ToDouble(greek.Vega));
                ThetaValues.Add(Convert.ToDouble(greek.Theta));
                RhoValues.Add(Convert.ToDouble(greek.Rho));
            }
        }

        private void InitialValues(DateTime date)
        {
            InitialDateTime = date;
            DeltaValues = new GearedValues<double>();
            GammaValues = new GearedValues<double>();
            VegaValues = new GearedValues<double>();
            ThetaValues = new GearedValues<double>();
            RhoValues = new GearedValues<double>();
        }
    }
}
