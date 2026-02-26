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
    public ICommand ShowSalesCommand { get; }
    public ICommand ShowPurchasesCommand { get; }
    public ICommand ShowDebtsCommand { get; }

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
        ShowSalesCommand = new RelayCommand(ShowSales);
        ShowPurchasesCommand = new RelayCommand(ShowPurchases);
        ShowDebtsCommand = new RelayCommand(ShowDebts);

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
    
    private void ShowSales()
    {
        _navigation.CurrentView = new Views.Sales.SalesView();
    }

    private void ShowPurchases()
    {
        _navigation.CurrentView = new Views.Purchases.PurchasesView();
    }

    private void ShowDebts()
    {
        _navigation.CurrentView = new Views.Debts.DebtsView();
    }
}