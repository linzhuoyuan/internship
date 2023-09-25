using System.Windows.Controls;

namespace Monitor.View.NewSession
{
    /// <summary>
    /// Interaction logic for NewFileSessionControl.xaml
    /// </summary>
    public partial class NewFileSessionControl : UserControl
    {
        public NewFileSessionControl()
        {
            InitializeComponent();
        }

        private void btnSelectFile_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            var ofd = new Microsoft.Win32.OpenFileDialog() {
                Filter = "File|*.db;*.*;"
            };
            ofd.Title = "Select File";
            ofd.Multiselect = false;
            if (ofd.ShowDialog() == true)
            {               
                txtFile.Text = ofd.FileName;
            }
            else
            { return; }
        }
    }
}
