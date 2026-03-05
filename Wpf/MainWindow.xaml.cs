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
            double from = SidebarColumn.Width.Value;
            double to = _sidebarOpened ? 60 : 220;

            var animation = new GridLengthAnimation
            {
                From = new GridLength(from),
                To = new GridLength(to),
                Duration = new Duration(TimeSpan.FromMilliseconds(250))
            };

            SidebarColumn.BeginAnimation(ColumnDefinition.WidthProperty, animation);

            if (_sidebarOpened)
            {
                DashboardText.Visibility = Visibility.Collapsed;
                ProductsText.Visibility = Visibility.Collapsed;
                SalesText.Visibility = Visibility.Collapsed;
                PurchasesText.Visibility = Visibility.Collapsed;
                DebtsText.Visibility = Visibility.Collapsed;
            }
            else
            {
                DashboardText.Visibility = Visibility.Visible;
                ProductsText.Visibility = Visibility.Visible;
                SalesText.Visibility = Visibility.Visible;
                PurchasesText.Visibility = Visibility.Visible;
                DebtsText.Visibility = Visibility.Visible;
            }

            _sidebarOpened = !_sidebarOpened;
        }
    }
}