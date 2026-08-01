using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using Application.Contracts.Interfaces;
using Wpf.Common;

namespace Wpf.ViewModels.Receipts;

/// <summary>Быстрый период списка чеков.</summary>
public enum ReceiptPeriod
{
    Today,
    Week,
    Month,
    Custom,
}

/// <summary>
/// Список чеков: поиск чека за нужную дату и просмотр его состава.
/// </summary>
public class ReceiptsListViewModel : ViewModelBase
{
    private static readonly CultureInfo Russian = new("ru-RU");

    private readonly ISalesService _sales;

    public ObservableCollection<ReceiptListItem> Receipts { get; } = new();

    public ICommand RefreshCommand { get; }
    public ICommand SelectCommand { get; }
    public ICommand CloseCommand { get; }
    public ICommand SetPeriodCommand { get; }
    public ICommand ClearSearchCommand { get; }

    public ReceiptsListViewModel(ISalesService sales)
    {
        _sales = sales;

        RefreshCommand = new AsyncRelayCommand(LoadAsync);
        SelectCommand = new AsyncRelayCommand<ReceiptListItem>(SelectAsync);
        CloseCommand = new RelayCommand(() => Selected = null);
        SetPeriodCommand = new RelayCommand<string>(SetPeriod);
        ClearSearchCommand = new RelayCommand(() => SearchText = "");

        SetPeriod(nameof(ReceiptPeriod.Today));
    }

    // ===================== Фильтры =====================

    private DateTime? _from;
    public DateTime? From
    {
        get => _from;
        set
        {
            if (SetProperty(ref _from, value))
                MarkCustomPeriod();
        }
    }

    private DateTime? _to;
    public DateTime? To
    {
        get => _to;
        set
        {
            if (SetProperty(ref _to, value))
                MarkCustomPeriod();
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

    private ReceiptPeriod _period = ReceiptPeriod.Today;
    public ReceiptPeriod Period
    {
        get => _period;
        private set
        {
            if (!SetProperty(ref _period, value))
                return;

            OnPropertyChanged(nameof(IsToday));
            OnPropertyChanged(nameof(IsWeek));
            OnPropertyChanged(nameof(IsMonth));
        }
    }

    public bool IsToday => Period == ReceiptPeriod.Today;
    public bool IsWeek => Period == ReceiptPeriod.Week;
    public bool IsMonth => Period == ReceiptPeriod.Month;

    // Даты меняли руками — быстрый период больше не подсвечиваем
    private bool _silentPeriodChange;

    private void MarkCustomPeriod()
    {
        if (_silentPeriodChange)
            return;

        Period = ReceiptPeriod.Custom;

        _ = LoadAsync();
    }

    private void SetPeriod(string? period)
    {
        if (!Enum.TryParse<ReceiptPeriod>(period, out var value))
            return;

        var today = DateTime.Today;

        _silentPeriodChange = true;

        From = value switch
        {
            ReceiptPeriod.Week  => today.AddDays(-6),
            ReceiptPeriod.Month => today.AddDays(-29),
            _                   => today,
        };

        To = today;

        _silentPeriodChange = false;

        Period = value;

        _ = LoadAsync();
    }

    // ===================== Список =====================

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

    private decimal _totalAmount;
    public decimal TotalAmount
    {
        get => _totalAmount;
        private set => SetProperty(ref _totalAmount, value);
    }

    public string SummaryText => Receipts.Count == 0
        ? "чеков нет"
        : $"{Receipts.Count} чек(ов) · {TotalAmount:N2}";

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

    public async Task LoadAsync()
    {
        IsLoading = true;

        try
        {
            var from = From is null ? (DateTimeOffset?)null : new DateTimeOffset(From.Value.Date);

            // Дата «по» включительно — верхняя граница на начало следующего дня
            var toExclusive = To is null ? (DateTimeOffset?)null : new DateTimeOffset(To.Value.Date.AddDays(1));

            var receipts = await _sales.GetReceiptsAsync(from, toExclusive, SearchText, CancellationToken.None);

            var selectedId = Selected?.SaleId;

            Receipts.Clear();

            foreach (var receipt in receipts)
                Receipts.Add(new ReceiptListItem(receipt));

            TotalAmount = Receipts.Sum(r => r.TotalAmount);
            IsEmpty = Receipts.Count == 0;
            ErrorMessage = "";

            // Открытый чек мог уйти из выборки — тогда закрываем карточку
            if (selectedId is { } id && Receipts.All(r => r.SaleId != id))
                Selected = null;

            OnPropertyChanged(nameof(SummaryText));
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Не удалось загрузить чеки: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    // ===================== Просмотр чека =====================

    private ReceiptListItem? _selectedRow;
    public ReceiptListItem? SelectedRow
    {
        get => _selectedRow;
        private set => SetProperty(ref _selectedRow, value);
    }

    private ReceiptDetailsViewModel? _selected;
    public ReceiptDetailsViewModel? Selected
    {
        get => _selected;
        private set
        {
            if (SetProperty(ref _selected, value))
                OnPropertyChanged(nameof(HasSelection));

            if (value is null)
                SelectedRow = null;
        }
    }

    public bool HasSelection => Selected is not null;

    private async Task SelectAsync(ReceiptListItem row)
    {
        SelectedRow = row;

        try
        {
            var receipt = await _sales.GetReceiptAsync(row.SaleId, CancellationToken.None);

            Selected = receipt is null ? null : new ReceiptDetailsViewModel(receipt);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Не удалось открыть чек: {ex.Message}";
        }
    }
}
