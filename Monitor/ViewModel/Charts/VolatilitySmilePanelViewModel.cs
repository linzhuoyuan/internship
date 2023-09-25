using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Messaging;
using LiveCharts;
using LiveCharts.Defaults;
using LiveCharts.Geared;
using LiveCharts.Wpf;
using Monitor.Model.Messages;
using Newtonsoft.Json;
using QuantConnect;
using QuantConnect.Logging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;
using System.ServiceModel;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Monitor.ViewModel.Charts
{
    public class VolatilitySmilePanelViewModel : ToolPaneViewModel
    {
        #region Properties

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

        private GearedValues<ObservablePoint> gValues00;
        public GearedValues<ObservablePoint> GValues00
        {
            get => gValues00;
            set
            {
                gValues00 = value;
                RaisePropertyChanged(() => GValues00);
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
        public VolatilitySmilePanelViewModel()
        {

        }

        private readonly IMessenger _messenger;

        #region Test

        private string info;
        public string Info
        {
            get => info;
            set
            {
                info = value;
                RaisePropertyChanged(() => Info);
            }
        }

        private string count;
        public string Count
        {
            get => count;
            set
            {
                count = value;
                RaisePropertyChanged(() => Count);
            }
        }

        CustomerChartData ccd = new CustomerChartData();

        #endregion

        bool once = false;
        public VolatilitySmilePanelViewModel(IMessenger messenger)
        {
            GPoint0 = new GearedValues<ObservablePoint>();
            GPoint1 = new GearedValues<ObservablePoint>();

            Name = "Volatility Smile";
            IsVisible = false;
            _messenger = messenger;
            _messenger.Register<List<CustomerChartData>>(this, "CustomerChart", lst =>
             {
                 if (lst.Count > 0)
                 {
                     if (!once)
                     {
                         IsVisible = true;
                         once = true;
                     }
                     try
                     {
                         ccd = lst[lst.Count - 1];
                     }
                     catch { }
                     ViewChartData(ccd);
                 }
             });
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="ccd"></param>
        private void ViewChartData(CustomerChartData ccd)
        {
            if (ccd != null && ccd.XList != null && ccd.XList.Length > 0)
            {
                try
                {
                    if (GPoint0.Count == ccd.XList.Length)
                    {
                        for (int i = 0; i < GPoint0.Count; i++)
                        {
                            GPoint0[i].X = ccd.XList[i];
                            GPoint0[i].Y = ccd.YList[i];

                            GPoint1[i].X = ccd.XList[i];
                            GPoint1[i].Y = ccd.Y1List[i];
                        }
                    }
                    else if (GPoint0.Count < ccd.XList.Length)
                    {
                        for (int i = 0; i < GPoint0.Count; i++)
                        {
                            GPoint0[i].X = ccd.XList[i];
                            GPoint0[i].Y = ccd.YList[i];

                            GPoint1[i].X = ccd.XList[i];
                            GPoint1[i].Y = ccd.Y1List[i];
                        }

                        for (int j = GPoint0.Count; j < ccd.XList.Length; j++)
                        {
                            GPoint0.Add(new ObservablePoint(ccd.XList[j], ccd.YList[j]));
                            GPoint1.Add(new ObservablePoint(ccd.XList[j], ccd.Y1List[j]));
                        }
                    }
                    else if (GPoint0.Count > ccd.XList.Length)
                    {
                        for (int i = 0; i < ccd.XList.Length; i++)
                        {
                            GPoint0[i].X = ccd.XList[i];
                            GPoint0[i].Y = ccd.YList[i];

                            GPoint1[i].X = ccd.XList[i];
                            GPoint1[i].Y = ccd.Y1List[i];
                        }

                        int count = GPoint0.Count;

                        for (int j = ccd.XList.Length; j < count; j++)
                        {
                            GPoint0.RemoveAt(ccd.XList.Length);
                            GPoint1.RemoveAt(ccd.XList.Length);
                        }
                    }
                    else
                    { }

                    TitleX = ccd.XLableName;
                    TitleY0 = ccd.YLableName;
                    TitleY1 = ccd.Y1LableName;
                    DateTime = ccd.DataTime.ToString();
                    MinValue = ccd.MinValue;
                    MaxValue = ccd.MaxValue;
                    MinXValue = ccd.MinXValue;
                    MaxXValue = ccd.MaxXValue;
                }
                catch (Exception e)
                {
                    Log.Error(e.Message);
                    GPoint0.Clear();
                    GPoint1.Clear();
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="ccd"></param>
        private void ViewChart(CustomerChartData ccd)
        {
            if (ccd != null && ccd.YList != null && ccd.YList.Length > 0)
            {
                GearedValues<double> gv1 = new GearedValues<double>();
                GearedValues<double> gv2 = new GearedValues<double>();
                GearedValues<ObservablePoint> gv01 = new GearedValues<ObservablePoint>();
                GearedValues<ObservablePoint> gv02 = new GearedValues<ObservablePoint>();

                try
                {
                    for (int i = 0; i < ccd.XList.Length; i++)
                    {
                        gv1.Add(ccd.YList[i]);
                        gv2.Add(ccd.Y1List[i]);
                        gv01.Add(new ObservablePoint(ccd.XList[i], ccd.YList[i]));
                        gv02.Add(new ObservablePoint(ccd.XList[i], ccd.Y1List[i]));
                    }

                    Labels = Array.ConvertAll<double, string>(ccd.XList, s => s.ToString());
                }
                catch { }

                GValues0 = gv1;
                GValues1 = gv2;

                TitleX = ccd.XLableName;
                TitleY0 = ccd.YLableName;
                TitleY1 = ccd.Y1LableName;
                DateTime = ccd.DataTime.ToString();
                MinValue = ccd.MinValue;
                MaxValue = ccd.MaxValue;
            }
            else
            { }
        }
    }
}
