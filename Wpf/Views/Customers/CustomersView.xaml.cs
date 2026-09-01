using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using Wpf.ViewModels.Customers;

namespace Wpf.Views.Customers
{
    public partial class CustomersView : UserControl
    {
        public CustomersView()
        {
            InitializeComponent();

            DataContext = App.Services.GetRequiredService<CustomersViewModel>();
        }
    }
}
