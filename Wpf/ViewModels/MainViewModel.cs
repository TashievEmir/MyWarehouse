using System.Windows.Input;
using Application.Contracts.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Wpf.Common;
using Wpf.Localization;
using Wpf.Services;
using Wpf.ViewModels.Products;
using Wpf.ViewModels.Statistics;

namespace Wpf.ViewModels;

public class MainViewModel : ViewModelBase
{
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

    // Храним ключи, а не готовый текст: при смене языка заголовок пересчитывается сам
    private string _titleKey = "";
    private string _subtitleKey = "";

    public string PageTitle => Loc.T(_titleKey);
    public string PageSubtitle => Loc.T(_subtitleKey);

    public string UserName => _session.DisplayName;
    public string UserRole => _session.RoleTitle;

    // ── Доступ по ролям ──
    //
    // Кассиру нужны только рабочие экраны: касса, каталог и приёмка.
    // Всё остальное — сводка, чеки, аналитика и настройки — админу и менеджеру.

    public bool CanSeeOverview => _session.IsPrivileged;
    public bool CanSeeReceipts => _session.IsPrivileged;
    public bool CanSeeAnalytics => _session.IsPrivileged;
    public bool CanSeeSettings => _session.IsPrivileged;

    /// <summary>Редактор шаблона чека доступен менеджеру и админу.</summary>
    public bool CanEditReceiptTemplate => _session.IsPrivileged;

    public string Today => DateTime.Now.ToString("d MMMM yyyy", Loc.Instance.Culture);

    public string ThemeLabel => _theme.ToggleLabel;

    /// <summary>Код языка, на который переключит кнопка: RU или KY.</summary>
    public string LanguageLabel => Loc.Instance.ToggleCode;

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
    public ICommand ShowNotificationsCommand { get; }
    public ICommand ShowUsersCommand { get; }
    public ICommand ToggleThemeCommand { get; }
    public ICommand ToggleLanguageCommand { get; }

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
            OnPropertyChanged(nameof(CanSeeOverview));
            OnPropertyChanged(nameof(CanSeeReceipts));
            OnPropertyChanged(nameof(CanSeeAnalytics));
            OnPropertyChanged(nameof(CanSeeSettings));
            OnPropertyChanged(nameof(CanEditReceiptTemplate));
        };

        _theme.PropertyChanged += (_, __) => OnPropertyChanged(nameof(ThemeLabel));

        // Строки страниц собраны в коде — после смены языка перезаходим на текущую
        Loc.LanguageChanged += () =>
        {
            OnPropertyChanged(nameof(LanguageLabel));
            OnPropertyChanged(nameof(ThemeLabel));
            OnPropertyChanged(nameof(Today));
            OnPropertyChanged(nameof(UserRole));

            _reopenCurrentPage?.Invoke();
        };

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
        ShowNotificationsCommand   = new RelayCommand(ShowNotifications);
        ShowUsersCommand           = new RelayCommand(ShowUsers);
        ToggleThemeCommand         = new RelayCommand(_theme.Toggle);
        ToggleLanguageCommand      = new RelayCommand(Loc.Instance.Toggle);

        // Кассиру сводка недоступна — открываем сразу кассу
        if (CanSeeOverview)
            ShowDashboard();
        else
            ShowSales();
    }

    // ── Обзор ──

    private void ShowDashboard()
    {
        if (!CanSeeOverview)
            return;

        Navigate(new Views.Dashboard.DashboardView(), "dashboard", "Page_Dashboard_Title", "Page_Dashboard_Sub", ShowDashboard);
    }

    private void ShowSales()
        => Navigate(new Views.Sales.SalesView(), "sales", "Page_Sales_Title", "Page_Sales_Sub", ShowSales);

    // ── Склад ──

    private void ShowCatalog()
    {
        Products().IsCatalogSelected = true;

        Navigate(new Views.Products.ProductsView(), "catalog", "Page_Catalog_Title", "Page_Catalog_Sub", ShowCatalog);
    }

    private void ShowReceiving()
    {
        Products().IsReceivingSelected = true;

        Navigate(new Views.Products.ProductsView(), "receiving", "Page_Receiving_Title", "Page_Receiving_Sub", ShowReceiving);
    }

    private void ShowReceipts()
    {
        if (!CanSeeReceipts)
            return;

        // Список перечитывается при каждом заходе: чеки пробивают прямо сейчас
        _ = App.Services.GetRequiredService<ViewModels.Receipts.ReceiptsListViewModel>().LoadAsync();

        Navigate(new Views.Receipts.ReceiptsListView(), "receipts", "Page_Receipts_Title", "Page_Receipts_Sub", ShowReceipts);
    }

    private void ShowReceiptTemplate()
    {
        if (!CanEditReceiptTemplate)
            return;

        _ = App.Services.GetRequiredService<ViewModels.Receipts.ReceiptTemplateViewModel>().LoadAsync();

        Navigate(new Views.Receipts.ReceiptTemplateView(), "editor", "Page_Editor_Title", "Page_Editor_Sub", ShowReceiptTemplate);
    }

    // ── Аналитика ──

    private void ShowActivityLog()
    {
        if (!CanSeeAnalytics)
            return;

        _ = App.Services.GetRequiredService<ViewModels.Activity.ActivityLogViewModel>().LoadAsync();

        Navigate(new Views.Activity.ActivityLogView(), "history", "Page_History_Title", "Page_History_Sub", ShowActivityLog);
    }

    private void ShowStock()
    {
        if (!CanSeeAnalytics)
            return;

        Statistics().IsStockSelected = true;

        Navigate(new Views.Statistics.StatisticsView(), "stock", "Page_Stock_Title", "Page_Stock_Sub", ShowStock);
    }

    private void ShowDebts()
    {
        if (!CanSeeAnalytics)
            return;

        Statistics().IsDebtsSelected = true;

        Navigate(new Views.Statistics.StatisticsView(), "debts", "Page_Debts_Title", "Page_Debts_Sub", ShowDebts);
    }

    private void ShowPurchases()
    {
        if (!CanSeeAnalytics)
            return;

        Statistics().IsPurchasesSelected = true;

        Navigate(new Views.Statistics.StatisticsView(), "purchases", "Page_Purchases_Title", "Page_Purchases_Sub", ShowPurchases);
    }

    // ── Настройки ──

    private void ShowNotifications()
    {
        if (!CanSeeSettings)
            return;

        _ = App.Services.GetRequiredService<ViewModels.Notifications.NotificationsViewModel>().LoadAsync();

        Navigate(new Views.Notifications.NotificationsView(), "notifications",
            "Page_Notifications_Title", "Page_Notifications_Sub", ShowNotifications);
    }

    private void ShowUsers()
    {
        if (!CanSeeSettings)
            return;

        _ = App.Services.GetRequiredService<ViewModels.Users.UsersViewModel>().LoadAsync();

        Navigate(new Views.Users.UsersView(), "users",
            "Page_Users_Title", "Page_Users_Sub", ShowUsers);
    }

    // ── Общее ──

    // Страницы держатся синглтонами: раздел выбирается навигацией до создания вью
    private static ProductsPageViewModel Products()
        => App.Services.GetRequiredService<ProductsPageViewModel>();

    private static StatisticsPageViewModel Statistics()
        => App.Services.GetRequiredService<StatisticsPageViewModel>();

    /// <summary>Повтор последнего перехода — нужен, чтобы перерисовать страницу на новом языке.</summary>
    private Action? _reopenCurrentPage;

    private void Navigate(object view, string pageKey, string titleKey, string subtitleKey, Action reopen)
    {
        _navigation.CurrentView = view;

        CurrentPage = pageKey;
        _titleKey = titleKey;
        _subtitleKey = subtitleKey;
        _reopenCurrentPage = reopen;

        OnPropertyChanged(nameof(PageTitle));
        OnPropertyChanged(nameof(PageSubtitle));

        _ = RefreshDebtsBadgeAsync();
    }

    /// <summary>
    /// Счётчик у пункта «Долги» обновляется при каждом переходе. Красным
    /// показываем только то, что требует внимания: срок наступил или прошёл.
    /// Долги без срока — из старых чеков — тоже считаем.
    /// </summary>
    private async Task RefreshDebtsBadgeAsync()
    {
        try
        {
            var debts = await _sales.GetDebtsAsync(null, CancellationToken.None);

            DebtsCount = debts.Count(d => d.DueDate is null
                                          || d.DueDate.Value.ToLocalTime().Date <= DateTime.Today);
        }
        catch
        {
            // счётчик — украшение: молчим, чтобы не мешать навигации
        }
    }
}
