using System.Collections.ObjectModel;
using System.Windows.Input;
using Application.Contracts.Interfaces;
using Wpf.Common;

using Wpf.Localization;

namespace Wpf.ViewModels.Statistics;

/// <summary>
/// Остатки по категориям за период: сколько товара есть сейчас
/// и сколько его поступило с даты «от» по дату «до».
/// </summary>
public class StockStatisticsViewModel : ViewModelBase
{
    private readonly IProductService _products;

    public ObservableCollection<CategoryStatItem> Stats { get; } = new();

    public ICommand RefreshCommand { get; }
    public ICommand ResetPeriodCommand { get; }

    public StockStatisticsViewModel(IProductService products)
    {
        _products = products;

        RefreshCommand = new AsyncRelayCommand(LoadAsync);
        ResetPeriodCommand = new RelayCommand(ResetPeriod);

        _ = LoadAsync();
    
        WatchLanguage();
    }

    // ===================== Период =====================

    private DateTime? _from;
    public DateTime? From
    {
        get => _from;
        set
        {
            if (SetProperty(ref _from, value))
                OnPropertyChanged(nameof(PeriodText));
        }
    }

    private DateTime? _to;
    public DateTime? To
    {
        get => _to;
        set
        {
            if (SetProperty(ref _to, value))
                OnPropertyChanged(nameof(PeriodText));
        }
    }

    public string PeriodText
    {
        get
        {
            if (From is null && To is null) return Loc.T("Period_All");
            if (From is not null && To is null) return Loc.F("Period_From", From.Value.ToString("dd.MM.yyyy", Loc.Instance.Culture));
            if (From is null && To is not null) return Loc.F("Period_To", To!.Value.ToString("dd.MM.yyyy", Loc.Instance.Culture));

            return Loc.F("Period_Range",
                From!.Value.ToString("dd.MM.yyyy", Loc.Instance.Culture),
                To!.Value.ToString("dd.MM.yyyy", Loc.Instance.Culture));
        }
    }

    // ===================== Состояние =====================

    private bool _isLoading;
    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
    }

    private bool _isEmpty;
    public bool IsEmpty
    {
        get => _isEmpty;
        private set => SetProperty(ref _isEmpty, value);
    }

    private int _totalInStock;
    public int TotalInStock
    {
        get => _totalInStock;
        private set => SetProperty(ref _totalInStock, value);
    }

    private int _totalReceived;
    public int TotalReceived
    {
        get => _totalReceived;
        private set => SetProperty(ref _totalReceived, value);
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

    // ===================== Загрузка =====================

    public async Task LoadAsync()
    {
        IsLoading = true;

        try
        {
            var from = From is null ? (DateTimeOffset?)null : new DateTimeOffset(From.Value.Date);

            // Дата «по» включительно — верхней границей берём начало следующего дня
            var toExclusive = To is null ? (DateTimeOffset?)null : new DateTimeOffset(To.Value.Date.AddDays(1));

            var categories = await _products.GetStockByCategoryAsync(from, toExclusive, CancellationToken.None);

            Stats.Clear();

            foreach (var category in categories)
                Stats.Add(new CategoryStatItem(category));

            TotalInStock = categories.Sum(c => c.InStock);
            TotalReceived = categories.Sum(c => c.Received);
            IsEmpty = Stats.Count == 0;

            ErrorMessage = "";
        }
        catch (Exception ex)
        {
            ErrorMessage = Loc.F("Stock_LoadFailed", ex.Message);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void ResetPeriod()
    {
        From = null;
        To = null;

        _ = LoadAsync();
    }

    /// <summary>Страница живёт синглтоном: после смены языка перечитываем её строки.</summary>
    private void WatchLanguage()
    {
        Loc.LanguageChanged += () =>
        {
            OnPropertyChanged(string.Empty);
            _ = LoadAsync();
        };
    }
}
