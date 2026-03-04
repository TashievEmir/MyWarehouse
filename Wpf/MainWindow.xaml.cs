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
            {
                SidebarColumn.Width = new GridLength(60);

                DashboardText.Visibility = Visibility.Collapsed;
                //ProductsText.Visibility = Visibility.Collapsed;
                //SalesText.Visibility = Visibility.Collapsed;
                //PurchasesText.Visibility = Visibility.Collapsed;
                //DebtsText.Visibility = Visibility.Collapsed;
            }
            else
            {
                SidebarColumn.Width = new GridLength(220);

                DashboardText.Visibility = Visibility.Visible;
                //ProductsText.Visibility = Visibility.Visible;
                //SalesText.Visibility = Visibility.Visible;
                //PurchasesText.Visibility = Visibility.Visible;
                //DebtsText.Visibility = Visibility.Visible;
            }

            _sidebarOpened = !_sidebarOpened;
        }
    }
}