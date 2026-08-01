using System.Globalization;
using System.Windows.Input;
using Application.Contracts.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Wpf.Common;
using Wpf.Services;
using Wpf.ViewModels.Products;
using Wpf.ViewModels.Statistics;

namespace Wpf.ViewModels;

public class MainViewModel : ViewModelBase
{
    private static readonly CultureInfo RussianCulture = new("ru-RU");

    private readonly NavigationService _navigation;
    private readonly SessionService _session;
    private readonly ThemeService _theme;
    private readonly ISalesService _sales;

    public object CurrentView => _navigation.CurrentView;

    private string _currentPage = "";
    /// <summary>Ключ активного листа меню — по нему подсвечивается пункт или подпункт.</summary>
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

    /// <summary>Редактор шаблона чека доступен менеджеру и админу.</summary>
    public bool CanEditReceiptTemplate
    {
        get
        {
            var roles = _session.User?.Roles;

            return roles is not null
                   && roles.Any(r => r is "Admin" or "Manager");
        }
    }

    public string Today => DateTime.Now.ToString("d MMMM yyyy", RussianCulture);

    public string ThemeLabel => _theme.ToggleLabel;

    // ── Долги: счётчик на пункте меню ──

    private int _debtsCount;
    public int DebtsCount
    {
        get => _debtsCount;
        private set
        {
            if (SetProperty(ref _debtsCount, value))
                OnPropertyChanged(nameof(HasDebts));
        }
    }

    public bool HasDebts => DebtsCount > 0;

    // ── Команды ──

    public ICommand ShowDashboardCommand { get; }
    public ICommand ShowSalesCommand { get; }
    public ICommand ShowCatalogCommand { get; }
    public ICommand ShowReceivingCommand { get; }
    public ICommand ShowReceiptsCommand { get; }
    public ICommand ShowReceiptTemplateCommand { get; }
    public ICommand ShowActivityLogCommand { get; }
    public ICommand ShowStockCommand { get; }
    public ICommand ShowDebtsCommand { get; }
    public ICommand ShowPurchasesCommand { get; }
    public ICommand ToggleThemeCommand { get; }

    public MainViewModel(
        NavigationService navigation,
        SessionService session,
        ThemeService theme,
        ISalesService sales)
    {
        _navigation = navigation;
        _session = session;
        _theme = theme;
        _sales = sales;

        _navigation.PropertyChanged += (_, __) => OnPropertyChanged(nameof(CurrentView));

        _session.PropertyChanged += (_, __) =>
        {
            OnPropertyChanged(nameof(UserName));
            OnPropertyChanged(nameof(UserRole));
            OnPropertyChanged(nameof(CanEditReceiptTemplate));
        };

        _theme.PropertyChanged += (_, __) => OnPropertyChanged(nameof(ThemeLabel));

        ShowDashboardCommand       = new RelayCommand(ShowDashboard);
        ShowSalesCommand           = new RelayCommand(ShowSales);
        ShowCatalogCommand         = new RelayCommand(ShowCatalog);
        ShowReceivingCommand       = new RelayCommand(ShowReceiving);
        ShowReceiptsCommand        = new RelayCommand(ShowReceipts);
        ShowReceiptTemplateCommand = new RelayCommand(ShowReceiptTemplate);
        ShowActivityLogCommand     = new RelayCommand(ShowActivityLog);
        ShowStockCommand           = new RelayCommand(ShowStock);
        ShowDebtsCommand           = new RelayCommand(ShowDebts);
        ShowPurchasesCommand       = new RelayCommand(ShowPurchases);
        ToggleThemeCommand         = new RelayCommand(_theme.Toggle);

        ShowDashboard();
    }

    // ── Обзор ──

    private void ShowDashboard()
        => Navigate(new Views.Dashboard.DashboardView(), "dashboard", "Главная", "Сводка дня: касса, склад и долги");

    private void ShowSales()
        => Navigate(new Views.Sales.SalesView(), "sales", "Касса", "Чеки, скидки и оплата");

    // ── Склад ──

    private void ShowCatalog()
    {
        Products().IsCatalogSelected = true;

        Navigate(new Views.Products.ProductsView(), "catalog", "Каталог товаров", "Карточки, цены, списание и удаление");
    }

    private void ShowReceiving()
    {
        Products().IsReceivingSelected = true;

        Navigate(new Views.Products.ProductsView(), "receiving", "Приёмка", "Приход товара по штрихкоду");
    }

    private void ShowReceipts()
        => Navigate(new Views.Common.ComingSoonView(), "receipts", "Список чеков", "Поиск чека, копия и возврат");

    private void ShowReceiptTemplate()
        => Navigate(new Views.Common.ComingSoonView(), "editor", "Редактор чека", "Что печатается на чеке и в каком порядке");

    // ── Аналитика ──

    private void ShowActivityLog()
        => Navigate(new Views.Common.ComingSoonView(), "history", "История действий", "Кто что сделал и когда");

    private void ShowStock()
    {
        Statistics().IsStockSelected = true;

        Navigate(new Views.Statistics.StatisticsView(), "stock", "Остатки", "Остатки по категориям за период");
    }

    private void ShowDebts()
    {
        Statistics().IsDebtsSelected = true;

        Navigate(new Views.Statistics.StatisticsView(), "debts", "Долги клиентов", "Незакрытые чеки и приём оплаты");
    }

    private void ShowPurchases()
    {
        Statistics().IsPurchasesSelected = true;

        Navigate(new Views.Statistics.StatisticsView(), "purchases", "Закупки", "Журнал поставок за период");
    }

    // ── Общее ──

    // Страницы держатся синглтонами: раздел выбирается навигацией до создания вью
    private static ProductsPageViewModel Products()
        => App.Services.GetRequiredService<ProductsPageViewModel>();

    private static StatisticsPageViewModel Statistics()
        => App.Services.GetRequiredService<StatisticsPageViewModel>();

    private void Navigate(object view, string pageKey, string title, string subtitle)
    {
        _navigation.CurrentView = view;

        CurrentPage = pageKey;
        PageTitle = title;
        PageSubtitle = subtitle;

        _ = RefreshDebtsBadgeAsync();
    }

    /// <summary>Счётчик у пункта «Долги» обновляется при каждом переходе.</summary>
    private async Task RefreshDebtsBadgeAsync()
    {
        try
        {
            var debts = await _sales.GetDebtsAsync(null, CancellationToken.None);

            DebtsCount = debts.Count;
        }
        catch
        {
            // счётчик — украшение: молчим, чтобы не мешать навигации
        }
    }
}
