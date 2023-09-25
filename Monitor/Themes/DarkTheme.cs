using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Monitor.Themes
{
    public class DarkTheme : AvalonDock.Themes.Theme
    {
        public override Uri GetResourceUri()
        {
            return new Uri(
                 "/DockThemes/DarkTheme.xaml",
                UriKind.Relative);
        }
    }
}
