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
        
        private void ToggleSidebar(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            double from = SidebarColumn.Width.Value;
            double to = _sidebarOpened ? 60 : 220; // закрытый / открытый размер

            var animation = new GridLengthAnimation
            {
                From = new GridLength(from),
                To = new GridLength(to),
                Duration = new Duration(TimeSpan.FromMilliseconds(200))
            };

            SidebarColumn.BeginAnimation(ColumnDefinition.WidthProperty, animation);

            _sidebarOpened = !_sidebarOpened;
        }
    }
}