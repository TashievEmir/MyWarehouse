using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using Application.Contracts.Interfaces;
using Wpf.Common;
using Wpf.Services;

using Wpf.Localization;

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
    private readonly SessionService _session;

    public ObservableCollection<ReceiptListItem> Receipts { get; } = new();

    public ICommand RefreshCommand { get; }
    public ICommand SelectCommand { get; }
    public ICommand CloseCommand { get; }
    public ICommand SetPeriodCommand { get; }
    public ICommand ClearSearchCommand { get; }
    public ICommand CopyCommand { get; }
    public ICommand ReturnCommand { get; }

    public ReceiptsListViewModel(ISalesService sales, SessionService session)
    {
        _sales = sales;
        _session = session;

        RefreshCommand = new AsyncRelayCommand(LoadAsync);
        SelectCommand = new AsyncRelayCommand<ReceiptListItem>(SelectAsync);
        CloseCommand = new RelayCommand(() => Selected = null);
        SetPeriodCommand = new RelayCommand<string>(SetPeriod);
        ClearSearchCommand = new RelayCommand(() => SearchText = "");
        CopyCommand = new RelayCommand(CopyToClipboard);
        ReturnCommand = new AsyncRelayCommand(ReturnAsync);

        SetPeriod(nameof(ReceiptPeriod.Today));
    }

    /// <summary>Возврат доступен менеджеру и админу; кассир только смотрит.</summary>
    public bool CanReturn
    {
        get
        {
            var roles = _session.User?.Roles;

            return roles is not null && roles.Any(r => r is "Admin" or "Manager");
        }
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
        ? Loc.T("Receipts_None")
        : Loc.F("Receipts_Summary", Receipts.Count, TotalAmount.ToString("N2", Loc.Instance.Culture));

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
            ErrorMessage = Loc.F("Receipts_LoadFailed", ex.Message);
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

    /// <summary>«Копия чека»: печати нет, поэтому кладём ленту в буфер обмена.</summary>
    private void CopyToClipboard()
    {
        if (Selected is null)
            return;

        try
        {
            Clipboard.SetText(Selected.AsText());

            ErrorMessage = "";
            StatusMessage = Loc.F("Receipts_Copied", Selected.SaleId);
        }
        catch (Exception ex)
        {
            ErrorMessage = Loc.F("Receipts_CopyFailed", ex.Message);
        }
    }

    private async Task ReturnAsync()
    {
        if (Selected is null)
            return;

        if (!CanReturn)
        {
            ErrorMessage = Loc.T("Receipts_ReturnDenied");
            return;
        }

        if (_session.User is null)
        {
            ErrorMessage = Loc.T("Receipts_ReturnNoLogin");
            return;
        }

        var answer = MessageBox.Show(
            Loc.F("Receipts_ReturnConfirm", Selected.SaleId, Selected.TotalAmount.ToString("N2", Loc.Instance.Culture)),
            Loc.T("Receipts_ReturnConfirmTitle"),
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (answer != MessageBoxResult.Yes)
            return;

        try
        {
            var saleId = Selected.SaleId;

            await _sales.ReturnSaleAsync(saleId, _session.User.UserId, CancellationToken.None);

            await LoadAsync();

            // Перечитываем открытый чек: он должен показаться уже возвращённым
            var refreshed = Receipts.FirstOrDefault(r => r.SaleId == saleId);

            if (refreshed is not null)
                await SelectAsync(refreshed);

            StatusMessage = Loc.F("Receipts_ReturnDone", saleId);
            ErrorMessage = "";
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    private string _statusMessage = "";
    public string StatusMessage
    {
        get => _statusMessage;
        private set
        {
            if (SetProperty(ref _statusMessage, value))
                OnPropertyChanged(nameof(HasStatus));
        }
    }

    public bool HasStatus => StatusMessage.Length > 0;

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
            ErrorMessage = Loc.F("Receipts_OpenFailed", ex.Message);
        }
    }
}
