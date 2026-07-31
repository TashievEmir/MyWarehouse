using System.Globalization;
using System.Windows.Input;
using Wpf.Common;
using Wpf.Services;

namespace Wpf.ViewModels;

public class MainViewModel : ViewModelBase
{
    private static readonly CultureInfo RussianCulture = new("ru-RU");

    private readonly NavigationService _navigation;
    private readonly SessionService _session;

    public object CurrentView => _navigation.CurrentView;

    private string _currentPage = "";
    /// <summary>Ключ активной страницы — по нему подсвечивается пункт меню.</summary>
    public string CurrentPage
    {
        get => _currentPage;
        private set => SetProperty(ref _currentPage, value);
    }

    private string _pageTitle = "";
    public string PageTitle
    {
        get => _pageTitle;
        private set => SetProperty(ref _pageTitle, value);
    }

    private string _pageSubtitle = "";
    public string PageSubtitle
    {
        get => _pageSubtitle;
        private set => SetProperty(ref _pageSubtitle, value);
    }

    public string UserName => _session.DisplayName;
    public string UserRole => _session.RoleTitle;

    public string Today => DateTime.Now.ToString("d MMMM yyyy", RussianCulture);

    public ICommand ShowDashboardCommand { get; }
    public ICommand ShowProductsCommand { get; }
    public ICommand ShowSalesCommand { get; }
    public ICommand ShowStatisticsCommand { get; }

    public MainViewModel(NavigationService navigation, SessionService session)
    {
        _navigation = navigation;
        _session = session;

        // 🔥 ВАЖНО
        _navigation.PropertyChanged += (_, __) =>
        {
            OnPropertyChanged(nameof(CurrentView));
        };

        _session.PropertyChanged += (_, __) =>
        {
            OnPropertyChanged(nameof(UserName));
            OnPropertyChanged(nameof(UserRole));
        };

        ShowDashboardCommand = new RelayCommand(ShowDashboard);
        ShowProductsCommand = new RelayCommand(ShowProducts);
        ShowSalesCommand = new RelayCommand(ShowSales);
        ShowStatisticsCommand = new RelayCommand(ShowStatistics);

        ShowDashboard();
    }

    private void ShowDashboard()
    {
        Navigate(new Views.Dashboard.DashboardView(), "Dashboard", "Главная", "Общее состояние склада");
    }

    private void ShowProducts()
    {
        Navigate(new Views.Products.ProductsView(), "Products", "Товары", "Каталог товаров и приёмка по штрихкоду");
    }

    private void ShowSales()
    {
        Navigate(new Views.Sales.SalesView(), "Sales", "Продажи", "Касса: чеки, скидки и оплата");
    }

    private void ShowStatistics()
    {
        Navigate(new Views.Statistics.StatisticsView(), "Statistics", "Статистика", "Остатки, долги клиентов и закупки");
    }

    private void Navigate(object view, string pageKey, string title, string subtitle)
    {
        _navigation.CurrentView = view;

        CurrentPage = pageKey;
        PageTitle = title;
        PageSubtitle = subtitle;
    }
}
