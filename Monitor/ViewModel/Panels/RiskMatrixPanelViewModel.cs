using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Messaging;
using LiveCharts;
using LiveCharts.Defaults;
using Monitor.Model;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace Monitor.ViewModel.Panels
{
    public class RiskMatrixPanelViewModel : ToolPaneViewModel
    {
        private string matrixName = "¥Delta";
        public string MatrixName
        {
            get => matrixName;
            set
            {
                matrixName = value;
                RaisePropertyChanged();
            }
        }

        private GreekItem greekValue;
        public GreekItem GreekValue
        {
            get => greekValue;
            set
            {
                greekValue = value;
                if (greekValue != null)
                {
                    //SplitGreeksTest();
                    MatrixName = greekValue.Name;
                    SelectionChanged(greekValue.Name);
                }
                RaisePropertyChanged(() => GreekValue);
            }
        }

        private ObservableCollection<GreekItem> greeks;
        public ObservableCollection<GreekItem> Greeks
        {
            get => greeks = new ObservableCollection<GreekItem>()
            {
                new GreekItem() { ID = 0, Name = "¥Delta" },
                new GreekItem() { ID = 1, Name = "¥Gamma 1%" },
                new GreekItem() { ID = 2, Name = "Vega" },
                new GreekItem() { ID = 3, Name = "Theta" },
                new GreekItem() { ID = 4, Name = "Rho" },
                new GreekItem() { ID = 5, Name = "PNL" }
            };
        }

        public string[] YTitle { get; set; } = { "2%", "1%", "0%", "-1%", "-2%" };
        public string[] XTitle { get; set; } = { "-2%", "-1%", "0%", "1%", "2%" };

        public ChartValues<HeatPoint> values;
        public ChartValues<HeatPoint> Values
        {
            get => values;
            set
            {
                values = value;
                RaisePropertyChanged(() => Values);
            }
        }

        private static int defaultLength = 5;

        private double rowHeight = 40d;
        public double RowHeight
        {
            get => rowHeight;
            set
            {
                rowHeight = value;
                RaisePropertyChanged(() => RowHeight);
            }
        }

        private decimal[,] DelteMatrix = new decimal[defaultLength, defaultLength];
        private decimal[,] GammaMatrix = new decimal[defaultLength, defaultLength];
        private decimal[,] VegaMatrix = new decimal[defaultLength, defaultLength];
        private decimal[,] ThetaMatrix = new decimal[defaultLength, defaultLength];
        private decimal[,] RhoMatrix = new decimal[defaultLength, defaultLength];
        private decimal[,] PnlMatrix = new decimal[defaultLength, defaultLength];

        private readonly IMessenger _messenger;
        public RiskMatrixPanelViewModel() { }

        public RiskMatrixPanelViewModel(IMessenger messenger)
        {
            Name = "Risk Matrix";
            GreekValue = new GreekItem() { ID = 0, Name = "¥Delta" };
            InitializeValues(DelteMatrix);
            _messenger = messenger;
            _messenger.Register<ImmediateOptionPriceModelResult[,]>(this, "matrix", matrix => {
                if (matrix != null)
                {
                    SplitGreeks(matrix);
                }
            });
        }

        private void InitializeMatrix(int length0, int length1)
        {
            if (length0 != defaultLength || length1 != defaultLength)
            {
                DelteMatrix = new decimal[length0, length1];
                GammaMatrix = new decimal[length0, length1];
                VegaMatrix = new decimal[length0, length1];
                ThetaMatrix = new decimal[length0, length1];
                RhoMatrix = new decimal[length0, length1];
                PnlMatrix = new decimal[length0, length1];
            }
        }

        int round = 2;
        private void SplitGreeks(ImmediateOptionPriceModelResult[,] matrix)
        {
            InitializeMatrix(matrix.GetLength(0), matrix.GetLength(1));
            for (int i = 0; i < matrix.GetLength(0); i++)
            {
                for (int j = 0; j < matrix.GetLength(1); j++)
                {
                    try
                    {
                        DelteMatrix[i, j] = Math.Round(matrix[j, i].Greeks.Delta, round);
                        GammaMatrix[i, j] = Math.Round(matrix[j, i].Greeks.Gamma, round);
                        VegaMatrix[i, j] = Math.Round(matrix[j, i].Greeks.Vega, round);
                        ThetaMatrix[i, j] = Math.Round(matrix[j, i].Greeks.Theta, round);
                        RhoMatrix[i, j] = Math.Round(matrix[j, i].Greeks.Rho, round);
                        PnlMatrix[i, j] = Math.Round(matrix[j, i].TheoreticalPrice, round);
                    }
                    catch { }
                }
            }
            if (!string.IsNullOrEmpty(MatrixName))
            {
                SelectionChanged(MatrixName);
            }
        }

        #region Test data

        Random ran = new Random((int)DateTime.Now.Ticks);
        private void SplitGreeksTest()
        {
            for (int i = 0; i < defaultLength; i++)
            {
                for (int j = 0; j < defaultLength; j++)
                {
                    try
                    {
                        DelteMatrix[i, j] = (decimal)Math.Round(ran.NextDouble(), 4);
                        GammaMatrix[i, j] = (decimal)Math.Round(ran.NextDouble(), 4);
                        VegaMatrix[i, j] = (decimal)Math.Round(ran.NextDouble(), 4);
                        ThetaMatrix[i, j] = (decimal)Math.Round(ran.NextDouble(), 4);
                        RhoMatrix[i, j] = (decimal)Math.Round(ran.NextDouble(), 4);
                        //DelteMatrix[i, j] = decimal.Parse(i.ToString() + j.ToString());
                        //GammaMatrix[i, j] = decimal.Parse(i.ToString() + j.ToString());
                        //VegaMatrix[i, j] = decimal.Parse(i.ToString() + j.ToString());
                        //ThetaMatrix[i, j] = decimal.Parse(i.ToString() + j.ToString());
                        //RhoMatrix[i, j] = decimal.Parse(i.ToString() + j.ToString());
                    }
                    catch { }
                }
            }
        }

        #endregion

        private void InitializeValues(decimal[,] data)
        {
            Application.Current.Dispatcher.Invoke(() => {
                if (Values == null)
                {
                    Values = new ChartValues<HeatPoint>();
                }
                if (Values != null && Values.Count != data.Length)
                {
                    Values.Clear();
                }

                for (int i = 0; i < data.GetLength(0); i++)
                {
                    for (int j = 0; j < data.GetLength(1); j++)
                    {
                        try
                        {
                            if (Values.Count < data.Length)
                            {
                                Values.Add(new HeatPoint(j, i, Convert.ToDouble(data[data.GetLength(0) - (i + 1), j])));
                            }
                            else
                            {
                                Values.FirstOrDefault(p => p.X == j && p.Y == i).Weight = Convert.ToDouble(data[data.GetLength(0) - (i + 1), j]);
                            }
                        }
                        catch { }
                    }
                }
            });
        }

        public void SelectionChanged(string greek)
        {
            if (!string.IsNullOrEmpty(greek))
            {
                decimal[,] data = null;
                switch (greek)
                {
                    case "¥Delta":
                        data = DelteMatrix;
                        break;
                    case "¥Gamma 1%":
                        data = GammaMatrix;
                        break;
                    case "Vega":
                        data = VegaMatrix;
                        break;
                    case "Theta":
                        data = ThetaMatrix;
                        break;
                    case "Rho":
                        data = RhoMatrix;
                        break;
                    case "PNL":
                        data = PnlMatrix;
                        break;
                    default:
                        break;
                };

                if (data != null)
                    InitializeValues(data);
            }
        }

        enum GreekName
        {
            Delta = 0,
            Gamma,
            Vega,
            Theta,
            Rho
        };
    }

    public class GreekItem
    {
        public int ID { get; set; }
        public string Name { get; set; }
    }
}
