using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Wpf.Common;
using Wpf.ViewModels;

namespace Wpf
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private bool _sidebarOpened = true;
        
        public MainWindow(MainViewModel vm)
        {
            InitializeComponent();
            DataContext = vm;
        }
        
        private void ToggleSidebar(object sender, RoutedEventArgs e)
        {
            if (_sidebarOpened)
                SidebarColumn.Width = new GridLength(0.5, GridUnitType.Star);
            else
                SidebarColumn.Width = new GridLength(1, GridUnitType.Star);

            _sidebarOpened = !_sidebarOpened;
        }
    }
}