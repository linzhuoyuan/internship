using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Monitor.View
{
    /// <summary>
    /// WinState.xaml 的交互逻辑
    /// </summary>
    public partial class WinState : UserControl
    {
        public WinState()
        {
            InitializeComponent();
            Loaded += WinState_Loaded;
        }

        Rect maxRect;
        Rect normalRect;
        Rect workArea = SystemParameters.WorkArea;
        double marginValue = 0d;//The window Content Margin

        private void WinState_Loaded(object sender, RoutedEventArgs e)
        {
            maxRect = new Rect(workArea.Left - marginValue, workArea.Top - marginValue, workArea.Width + marginValue * 2, workArea.Height + marginValue * 2);
            try
            {
                if (_window == null)
                {
                    _window = Window.GetWindow(this);
                }
                _window.SizeChanged += _window_SizeChanged;
            }
            catch
            { }
            if (!canResize)
            {
                btnResize.Visibility = Visibility.Collapsed;
                btnMinimize.Visibility = Visibility.Collapsed;
            }
        }

        private void _window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_window.WindowState == WindowState.Maximized)
            {
                _window.WindowState = WindowState.Normal;
                MoveWindow(maxRect);
            }
        }

        private Window _window;
        public Window Window
        {
            get { return _window; }
            set
            {
                _window = value;
            }
        }

        private bool isExit = false;
        public bool IsExit
        {
            get { return isExit; }
            set { isExit = value; }
        }

        private bool canResize = true;
        public bool CanResize
        {
            get { return canResize; }
            set { canResize = value; }
        }

        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            if (isExit)
            {
                Application.Current.Shutdown();
                //Environment.Exit(0); //Force Close.
            }
            else
            {
                _window.Close();
            }
        }

        private void btnMinimize_Click(object sender, RoutedEventArgs e)
        {
            _window.WindowState = WindowState.Minimized;
        }

        private void btnResize_Click(object sender, RoutedEventArgs e)
        {
            if (_window.WindowState == WindowState.Maximized)
            {
                _window.WindowState = WindowState.Normal;
                if (normalRect != null)
                {
                    MoveWindow(normalRect);
                }
            }
            else
            {
                if (_window.Width < workArea.Width || _window.Height < workArea.Height)
                {
                    normalRect = new Rect(_window.Left, _window.Top, _window.Width, _window.Height);
                    MoveWindow(maxRect);
                }
                else
                {
                    if (normalRect != null)
                    {
                        MoveWindow(normalRect);
                    }
                }
            }
            Resize?.Invoke(sender, e);
        }

        public event EventHandler Resize;

        public void MoveWindow(Rect rect)
        {
            _window.Top = rect.Y;
            _window.Left = rect.X;
            _window.Height = rect.Height;
            _window.Width = rect.Width;
        }
    }
}
