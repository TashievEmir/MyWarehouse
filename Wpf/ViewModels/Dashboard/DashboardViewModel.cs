using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using Application.Contracts.Interfaces;
using Wpf.Common;
using Wpf.Services;

namespace Wpf.ViewModels.Dashboard;

/// <summary>
/// Главная страница: касса за сегодня, динамика выручки, долги и то,
/// что заканчивается на складе.
/// </summary>
public class DashboardViewModel : ViewModelBase
{
    private static readonly CultureInfo Russian = new("ru-RU");

    private const int RevenueDays = 14;
    private const int TopDays = 7;
    private const int LowStockThreshold = 5;

    private readonly IDashboardService _dashboard;
    private readonly SessionService _session;

    public ObservableCollection<RevenueBarItem> Revenue { get; } = new();
    public ObservableCollection<PaymentSliceItem> Payments { get; } = new();
    public ObservableCollection<LowStockItem> LowStock { get; } = new();
    public ObservableCollection<TopProductItem> TopProducts { get; } = new();

    public ICommand RefreshCommand { get; }

    public DashboardViewModel(IDashboardService dashboard, SessionService session)
    {
        _dashboard = dashboard;
        _session = session;

        RefreshCommand = new AsyncRelayCommand(LoadAsync);

        _ = LoadAsync();
    }

    // ===================== Шапка =====================

    public string Greeting
    {
        get
        {
            var name = _session.User?.Firstname;

            var hour = DateTime.Now.Hour;

            string part = hour switch
            {
                >= 5 and < 12  => "Доброе утро",
                >= 12 and < 18 => "Добрый день",
                >= 18 and < 23 => "Добрый вечер",
                _              => "Доброй ночи",
            };

            return string.IsNullOrWhiteSpace(name) ? part : $"{part}, {name}";
        }
    }

    public string TodayLabel => DateTime.Now.ToString("d MMMM yyyy", Russian);

    public string RevenuePeriodLabel => $"Выручка за {RevenueDays} дней";

    public string TopPeriodLabel => $"Топ товаров за {TopDays} дней";

    public string LowStockLabel => $"Заканчивается (≤ {LowStockThreshold} шт.)";

    // ===================== Показатели =====================

    private decimal _todayRevenue;
    public decimal TodayRevenue
    {
        get => _todayRevenue;
        private set => SetProperty(ref _todayRevenue, value);
    }

    private int _todayReceipts;
    public int TodayReceipts
    {
        get => _todayReceipts;
        private set => SetProperty(ref _todayReceipts, value);
    }

    private decimal _averageReceipt;
    public decimal AverageReceipt
    {
        get => _averageReceipt;
        private set => SetProperty(ref _averageReceipt, value);
    }

    private decimal _todayProfit;
    public decimal TodayProfit
    {
        get => _todayProfit;
        private set
        {
            if (SetProperty(ref _todayProfit, value))
                OnPropertyChanged(nameof(IsProfitPositive));
        }
    }

    public bool IsProfitPositive => TodayProfit >= 0;

    private decimal _todayPurchases;
    public decimal TodayPurchases
    {
        get => _todayPurchases;
        private set => SetProperty(ref _todayPurchases, value);
    }

    private int _todayWrittenOff;
    public int TodayWrittenOff
    {
        get => _todayWrittenOff;
        private set => SetProperty(ref _todayWrittenOff, value);
    }

    private decimal _totalDebt;
    public decimal TotalDebt
    {
        get => _totalDebt;
        private set => SetProperty(ref _totalDebt, value);
    }

    private int _debtorsCount;
    public int DebtorsCount
    {
        get => _debtorsCount;
        private set => SetProperty(ref _debtorsCount, value);
    }

    public string DebtorsLabel => DebtorsCount == 0 ? "долгов нет" : $"{DebtorsCount} незакрытых чек(ов)";

    // ===================== Требует внимания =====================

    private int _productsWithoutPrice;
    public int ProductsWithoutPrice
    {
        get => _productsWithoutPrice;
        private set
        {
            if (SetProperty(ref _productsWithoutPrice, value))
            {
                OnPropertyChanged(nameof(HasPriceWarning));
                OnPropertyChanged(nameof(PriceWarningText));
            }
        }
    }

    public bool HasPriceWarning => ProductsWithoutPrice > 0;

    public string PriceWarningText =>
        $"{ProductsWithoutPrice} товар(ов) без цены продажи — их нельзя пробить на кассе. Проставьте цену в «Товары → Каталог»";

    private int _productsOutOfStock;
    public int ProductsOutOfStock
    {
        get => _productsOutOfStock;
        private set => SetProperty(ref _productsOutOfStock, value);
    }

    // ===================== Состояние =====================

    private bool _isLoading;
    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
    }

    private string _errorMessage = "";
    public string ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (SetProperty(ref _errorMessage, value))
                OnPropertyChanged(nameof(HasError));
        }
    }

    public bool HasError => ErrorMessage.Length > 0;

    public bool HasSales => TodayReceipts > 0;

    public bool HasLowStock => LowStock.Count > 0;

    public bool HasTopProducts => TopProducts.Count > 0;

    // ===================== Загрузка =====================

    public async Task LoadAsync()
    {
        IsLoading = true;

        try
        {
            var snapshot = await _dashboard.GetSnapshotAsync(
                RevenueDays, TopDays, LowStockThreshold, CancellationToken.None);

            TodayRevenue    = snapshot.TodayRevenue;
            TodayReceipts   = snapshot.TodayReceipts;
            AverageReceipt  = snapshot.AverageReceipt;
            TodayProfit     = snapshot.TodayProfit;
            TodayPurchases  = snapshot.TodayPurchases;
            TodayWrittenOff = snapshot.TodayWrittenOff;

            TotalDebt    = snapshot.TotalDebt;
            DebtorsCount = snapshot.DebtorsCount;

            ProductsWithoutPrice = snapshot.ProductsWithoutPrice;
            ProductsOutOfStock   = snapshot.ProductsOutOfStock;

            var maxRevenue = snapshot.Revenue.Count == 0 ? 0m : snapshot.Revenue.Max(d => d.Amount);

            Revenue.Clear();
            foreach (var day in snapshot.Revenue)
                Revenue.Add(new RevenueBarItem(day, maxRevenue));

            Payments.Clear();
            foreach (var slice in snapshot.Payments)
                Payments.Add(new PaymentSliceItem(slice, snapshot.TodayRevenue));

            LowStock.Clear();
            foreach (var product in snapshot.LowStock)
                LowStock.Add(new LowStockItem(product));

            TopProducts.Clear();
            foreach (var product in snapshot.TopProducts)
                TopProducts.Add(new TopProductItem(product, TopProducts.Count + 1));

            ErrorMessage = "";

            OnPropertyChanged(nameof(Greeting));
            OnPropertyChanged(nameof(DebtorsLabel));
            OnPropertyChanged(nameof(HasSales));
            OnPropertyChanged(nameof(HasLowStock));
            OnPropertyChanged(nameof(HasTopProducts));
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Не удалось загрузить сводку: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }
}
