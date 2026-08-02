using System.Collections.ObjectModel;
using System.Windows.Input;
using Application.Contracts.Interfaces;
using Wpf.Common;

using Wpf.Localization;

namespace Wpf.ViewModels.Statistics;

/// <summary>
/// Журнал закупок: какие поставки были за период, от кого и на какую сумму.
/// Сами поставки создаются приёмкой на странице «Товары».
/// </summary>
public class PurchaseLogViewModel : ViewModelBase
{
    private readonly IPurchaseService _purchases;

    public ObservableCollection<PurchaseLogItem> Purchases { get; } = new();

    public ICommand RefreshCommand { get; }
    public ICommand ResetPeriodCommand { get; }
    public ICommand ClearSearchCommand { get; }

    public PurchaseLogViewModel(IPurchaseService purchases)
    {
        _purchases = purchases;

        RefreshCommand = new AsyncRelayCommand(LoadAsync);
        ResetPeriodCommand = new RelayCommand(ResetPeriod);
        ClearSearchCommand = new RelayCommand(() => SearchText = "");
    
        WatchLanguage();
    }

    // ===================== Период и поиск =====================

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

    private string _searchText = "";
    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                OnPropertyChanged(nameof(HasSearch));
                _ = LoadAsync();
            }
        }
    }

    public bool HasSearch => SearchText.Length > 0;

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

    private decimal _totalCost;
    /// <summary>Сколько потрачено на закупки за период.</summary>
    public decimal TotalCost
    {
        get => _totalCost;
        private set => SetProperty(ref _totalCost, value);
    }

    private int _totalItems;
    public int TotalItems
    {
        get => _totalItems;
        private set
        {
            if (SetProperty(ref _totalItems, value))
                OnPropertyChanged(nameof(TotalItemsText));
        }
    }

    public string TotalItemsText => Loc.F("Purchases_ItemsCount", TotalItems);

    public string CountText => Purchases.Count == 0 ? "" : Loc.F("Purchases_Count", Purchases.Count);

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

            var purchases = await _purchases.GetPurchasesAsync(from, toExclusive, SearchText, CancellationToken.None);

            Purchases.Clear();

            foreach (var purchase in purchases)
                Purchases.Add(new PurchaseLogItem(purchase));

            TotalCost = Purchases.Sum(p => p.TotalCost);
            TotalItems = Purchases.Sum(p => p.ItemsCount);
            IsEmpty = Purchases.Count == 0;

            ErrorMessage = "";

            OnPropertyChanged(nameof(CountText));
        }
        catch (Exception ex)
        {
            ErrorMessage = Loc.F("Purchases_LoadFailed", ex.Message);
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
