using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using Wpf.ViewModels.Products;

namespace Wpf.Views.Products;

public partial class ProductsView : UserControl
{
    public ProductsView()
    {
        InitializeComponent();
        DataContext = App.Services.GetRequiredService<ProductsViewModel>();
    }
}