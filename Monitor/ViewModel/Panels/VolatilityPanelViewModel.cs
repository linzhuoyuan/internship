using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Messaging;
using LiveCharts.Defaults;
using LiveCharts.Geared;
using LiveCharts.Helpers;
using QuantConnect;
using QuantConnect.Logging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace Monitor.ViewModel.Panels
{
    public class VolatilityPanelViewModel : ToolPaneViewModel
    {
        private ObservableCollection<VolatilityModel> _volatilities;
        public ObservableCollection<VolatilityModel> Volatilities
        {
            get => _volatilities;
            set
            {
                _volatilities = value;
                RaisePropertyChanged(() => _volatilities);
            }
        }

        private readonly IMessenger _messenger;
        private bool _once = false;

        public VolatilityPanelViewModel() { }
        public VolatilityPanelViewModel(IMessenger messenger)
        {
            Name = "Volatility";
            IsVisible = false;
            _messenger = messenger;
            Volatilities = new ObservableCollection<VolatilityModel>();
            _messenger.Register<List<CustomerChartData>>(this, "CustomerChart", ccds =>
            {
                if (ccds != null && ccds.Count > 0)
                {
                    if (!_once)
                    {
                        IsVisible = true;
                        _once = true;
                    }

                    SetVolatiles(ccds);
                }
            });
        }

        #region Set Row Count
        public int RowCount { get; set; } = 1;
        public int ColCount { get; set; }

        private void SetRows(int count)
        {
            if (count <= 2)
            {
                RowCount = 1;
            }
            else if (count > 2 && count <= 8)
            {
                RowCount = 2;
            }
            else if (count > 8 && count < 16)
            {
                RowCount = 3;
            }
            else
            {
                RowCount = 4;
            }

            Application.Current.Resources["Rows"] = RowCount;
        }
        #endregion

        readonly List<string> _names = new List<string>();
        private void SetVolatiles(List<CustomerChartData> ccds)
        {
            //Application.Current?.Dispatcher.Invoke(() =>
            //{
            //    Volatilities?.Clear();
            //});

            //List<CustomerChartData> uniqueData = ccds.Where((obj, i) => ccds.FindIndex(obj1 => obj1.Id == obj.Id) == i).ToList();

            if (Volatilities.Count > 0)
            {
                foreach (var model in Volatilities)
                {
                    if (!ccds.Exists(c => c.Name.Equals(model.Name)))
                    {
                        _names.Add(model.Name);
                    }
                }

                if (_names.Count > 0)
                {
                    _names.ForEach(n =>
                    {
                        Application.Current?.Dispatcher.Invoke(() =>
                        {
                            Volatilities.Remove(Volatilities.FirstOrDefault(v => v.Name.Equals(n)));
                        });
                    });

                    _names.Clear();
                }
            }

            Dictionary<string, CustomerChartData> uniqueData = new Dictionary<string, CustomerChartData>();
            ccds.ForEach(ccd =>
            {
                if (!uniqueData.Keys.Contains(ccd.Name))
                {
                    uniqueData.Add(ccd.Name, ccd);
                }
            });
            if (uniqueData.Count > 0)
            {
                SetRows(uniqueData.Count);
                ViewChartData(uniqueData);
            }
        }

        private void ViewChartData(Dictionary<string, CustomerChartData> pairs)
        {
            foreach (var pair in pairs)
            {
                if (Volatilities.FirstOrDefault(v => v.Name.Equals(pair.Key)) != null)
                {
                    UpdateData(pair.Value);
                }
                else
                {
                    AddData(pair.Value);
                }
            }
        }

        private void UpdateData(CustomerChartData ccd)
        {
            foreach (var model in Volatilities)
            {
                if (model.Name == ccd.Name)
                {
                    try
                    {
                        if (model.GPoint0.Count == ccd.XList.Length)
                        {
                            for (var i = 0; i < model.GPoint0.Count; i++)
                            {
                                model.GPoint0[i].X = ccd.XList[i];
                                model.GPoint0[i].Y = ccd.YList[i];

                                model.GPoint1[i].X = ccd.XList[i];
                                model.GPoint1[i].Y = ccd.Y1List[i];
                            }
                        }
                        else if (model.GPoint0.Count < ccd.XList.Length)
                        {
                            for (var i = 0; i < model.GPoint0.Count; i++)
                            {
                                model.GPoint0[i].X = ccd.XList[i];
                                model.GPoint0[i].Y = ccd.YList[i];

                                model.GPoint1[i].X = ccd.XList[i];
                                model.GPoint1[i].Y = ccd.Y1List[i];
                            }

                            for (var j = model.GPoint0.Count; j < ccd.XList.Length; j++)
                            {
                                model.GPoint0.Add(new ObservablePoint(ccd.XList[j], ccd.YList[j]));
                                model.GPoint1.Add(new ObservablePoint(ccd.XList[j], ccd.Y1List[j]));
                            }
                        }
                        else if (model.GPoint0.Count > ccd.XList.Length)
                        {
                            for (var i = 0; i < ccd.XList.Length; i++)
                            {
                                model.GPoint0[i].X = ccd.XList[i];
                                model.GPoint0[i].Y = ccd.YList[i];

                                model.GPoint1[i].X = ccd.XList[i];
                                model.GPoint1[i].Y = ccd.Y1List[i];
                            }

                            var count = model.GPoint0.Count;

                            for (var j = ccd.XList.Length; j < count; j++)
                            {
                                model.GPoint0.RemoveAt(ccd.XList.Length);
                                model.GPoint1.RemoveAt(ccd.XList.Length);
                            }
                        }

                        SetVolatilityFields(model, ccd);
                    }
                    catch (Exception e)
                    {
                        Log.Error(e.Message);
                        model.GPoint0.Clear();
                        model.GPoint1.Clear();
                    }
                }
            }
        }

        private void AddData(CustomerChartData ccd)
        {
            VolatilityModel v = null;
            if (ccd != null && ccd.XList != null && ccd.XList.Length > 0)
            {
                v = new VolatilityModel();
                v.GPoint0 = new GearedValues<ObservablePoint>();
                v.GPoint1 = new GearedValues<ObservablePoint>();
                SetVolatilityFields(v, ccd);
                for (int i = 0; i < ccd.XList.Length; i++)
                {
                    v.GPoint0.Add(new ObservablePoint(ccd.XList[i], ccd.YList[i]));
                    v.GPoint1.Add(new ObservablePoint(ccd.XList[i], ccd.Y1List[i]));
                }

                Application.Current.Dispatcher.Invoke(() =>
                {
                    Volatilities.Add(v);
                });
            }
        }

        private void SetVolatilityFields(VolatilityModel v, CustomerChartData ccd)
        {
            if (v != null && ccd != null)
            {
                v.Name = ccd.Name;
                v.TitleX = ccd.XLableName;
                v.TitleY0 = ccd.YLableName;
                v.TitleY1 = ccd.Y1LableName;
                v.DateTime = ccd.DataTime.ToString();
                v.MinValue = ccd.MinValue;
                v.MaxValue = ccd.MaxValue;
                v.MinXValue = ccd.MinXValue;
                v.MinXValue = ccd.MinXValue;
                v.MaxXValue = ccd.MaxXValue;
            }
        }
    }

    public class VolatilityModel : ViewModelBase
    {
        #region Properties

        private string id;
        public string ID
        {
            get => id;
            set
            {
                id = value;
                RaisePropertyChanged();
            }
        }

        private string name;
        public string Name
        {
            get => name;
            set
            {
                name = value;
                RaisePropertyChanged();
            }
        }

        private double minValue = 0;
        public double MinValue
        {
            get => minValue;
            set
            {
                minValue = value;
                RaisePropertyChanged();
            }
        }

        private double maxValue = 2.1;
        public double MaxValue
        {
            get => maxValue;
            set
            {
                maxValue = value + .1d;
                RaisePropertyChanged();
            }
        }

        private double minXValue;
        public double MinXValue
        {
            get => minXValue;
            set
            {
                minXValue = value;
                RaisePropertyChanged();
            }
        }

        private double maxXValue;
        public double MaxXValue
        {
            get => maxXValue;
            set
            {
                maxXValue = value;
                RaisePropertyChanged();
            }
        }

        private string titlex;
        public string TitleX
        {
            get { return titlex; }
            set { titlex = value; RaisePropertyChanged(); }
        }

        private string titleY0;
        public string TitleY0
        {
            get => titleY0;
            set
            {
                titleY0 = value;
                RaisePropertyChanged(() => TitleY0);
            }
        }

        private string titleY1;
        public string TitleY1
        {
            get => titleY1;
            set
            {
                titleY1 = value;
                RaisePropertyChanged(() => TitleY1);
            }
        }

        private string[] labels;
        public string[] Labels
        {
            get => labels;
            set
            {
                labels = value;
                RaisePropertyChanged(() => Labels);
            }
        }

        private GearedValues<double> gValues0;
        public GearedValues<double> GValues0
        {
            get => gValues0;
            set
            {
                gValues0 = value;
                RaisePropertyChanged(() => GValues0);
            }
        }

        private GearedValues<double> gValues1;
        public GearedValues<double> GValues1
        {
            get => gValues1;
            set
            {
                gValues1 = value;
                RaisePropertyChanged(() => GValues1);
            }
        }

        private GearedValues<ObservablePoint> gPoint0;
        public GearedValues<ObservablePoint> GPoint0
        {
            get => gPoint0;
            set
            {
                gPoint0 = value;
                RaisePropertyChanged(() => GPoint0);
            }
        }

        private GearedValues<ObservablePoint> gPoint1;
        public GearedValues<ObservablePoint> GPoint1
        {
            get => gPoint1;
            set
            {
                gPoint1 = value;
                RaisePropertyChanged(() => GPoint1);
            }
        }

        private string dateTime;
        public string DateTime
        {
            get => dateTime;
            set
            {
                dateTime = value;
                RaisePropertyChanged(() => DateTime);
            }
        }

        #endregion
    }
}
