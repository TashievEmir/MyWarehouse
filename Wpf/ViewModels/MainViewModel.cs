using System.Windows.Input;
using Wpf.Common;
using Wpf.Services;

namespace Wpf.ViewModels;

public class MainViewModel : ViewModelBase
{
    private readonly NavigationService _navigation;

    public object CurrentView => _navigation.CurrentView;

    public ICommand ShowDashboardCommand { get; }
    public ICommand ShowProductsCommand { get; }

    public MainViewModel(NavigationService navigation)
    {
        _navigation = navigation;

        // 🔥 ВАЖНО
        _navigation.PropertyChanged += (_, __) =>
        {
            OnPropertyChanged(nameof(CurrentView));
        };

        ShowDashboardCommand = new RelayCommand(ShowDashboard);
        ShowProductsCommand = new RelayCommand(ShowProducts);

        ShowDashboard();
    }

    private void ShowDashboard()
    {
        _navigation.CurrentView = new Views.Dashboard.DashboardView();
    }

    private void ShowProducts()
    {
        _navigation.CurrentView = new Views.Products.ProductsView();
    }
}