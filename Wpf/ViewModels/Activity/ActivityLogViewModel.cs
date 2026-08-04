using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using Application.Contracts.Interfaces;
using Application.DTOs.Activity;
using Domain.Enums;
using Wpf.Common;
using Wpf.Localization;

namespace Wpf.ViewModels.Activity;

/// <summary>Запись таймлайна: иконка и цвет зависят от типа события.</summary>
public class ActivityEntryItem
{
    private static CultureInfo Ui => Wpf.Localization.Loc.Instance.Culture;

    public DateTimeOffset OccurredAt { get; }
    public string TimeText => OccurredAt.ToLocalTime().ToString("HH:mm", Ui);

    public string UserName { get; }
    public string Title { get; }
    public string? Details { get; }

    public bool HasDetails => !string.IsNullOrWhiteSpace(Details);

    public string Icon { get; }
    public string ColorKey { get; }

    public ActivityEntryItem(ActivityEntryResponse entry)
    {
        OccurredAt = entry.OccurredAt;
        UserName = entry.UserName;
        Title = entry.Title;
        Details = entry.Details;

        (Icon, ColorKey) = entry.Type switch
        {
            ActivityType.SaleClosed      => ("ReceiptTextCheckOutline", "SuccessBrush"),
            ActivityType.PriceChanged    => ("TagOutline",              "WarningBrush"),
            ActivityType.CreditSale      => ("CreditCardClockOutline",  "DangerBrush"),
            ActivityType.PurchaseSaved   => ("DownloadOutline",         "PrimaryBrush"),
            ActivityType.StockWrittenOff => ("TrashCanOutline",         "DangerBrush"),
            ActivityType.DebtPaid        => ("CashCheck",               "SuccessBrush"),
            ActivityType.ProductCreated  => ("PlusBoxOutline",          "PrimaryBrush"),
            ActivityType.SaleReturned    => ("CloseCircleOutline",      "DangerBrush"),
            ActivityType.TemplateSaved   => ("ContentSaveOutline",      "PrimaryBrush"),
            ActivityType.StockAdjusted   => ("ClipboardEditOutline",    "WarningBrush"),
            ActivityType.UserCreated     => ("AccountPlusOutline",      "PrimaryBrush"),
            ActivityType.UserUpdated     => ("AccountEditOutline",      "WarningBrush"),
            ActivityType.UserDeleted     => ("AccountRemoveOutline",    "DangerBrush"),
            _                            => ("Login",                   "MutedBrush"),
        };
    }
}

/// <summary>
/// История действий: кто что сделал, с фильтром по периоду и пользователю.
/// </summary>
public class ActivityLogViewModel : ViewModelBase
{
    private static CultureInfo Ui => Wpf.Localization.Loc.Instance.Culture;

    private readonly IActivityLogService _activity;

    public ObservableCollection<ActivityEntryItem> Entries { get; } = new();
    public ObservableCollection<ActivityUserResponse> Users { get; } = new();

    public ICommand RefreshCommand { get; }
    public ICommand ClearSearchCommand { get; }

    public ActivityLogViewModel(IActivityLogService activity)
    {
        _activity = activity;

        RefreshCommand = new AsyncRelayCommand(LoadAsync);
        ClearSearchCommand = new RelayCommand(() => SearchText = "");

        From = DateTime.Today;
        To = DateTime.Today;
    }

    // ===================== Фильтры =====================

    private DateTime? _from;
    public DateTime? From
    {
        get => _from;
        set
        {
            if (SetProperty(ref _from, value))
                OnPropertyChanged(nameof(PeriodTitle));
        }
    }

    private DateTime? _to;
    public DateTime? To
    {
        get => _to;
        set
        {
            if (SetProperty(ref _to, value))
                OnPropertyChanged(nameof(PeriodTitle));
        }
    }

    public string PeriodTitle
    {
        get
        {
            // Культуру задаём явно: у интерполяции она берётся с потока и зависит от системы
            if (From is null && To is null) return Loc.T("Activity_PeriodAll");

            if (From is not null && From == To)
                return Loc.F("Activity_PeriodDay", From.Value.ToString("d MMMM yyyy", Ui));

            if (From is not null && To is not null)
                return Loc.F("Activity_PeriodRange",
                    From.Value.ToString("dd.MM.yyyy", Ui),
                    To.Value.ToString("dd.MM.yyyy", Ui));

            return From is not null
                ? Loc.F("Activity_PeriodFrom", From.Value.ToString("dd.MM.yyyy", Ui))
                : Loc.F("Activity_PeriodTo", To!.Value.ToString("dd.MM.yyyy", Ui));
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

    private ActivityUserResponse? _selectedUser;
    public ActivityUserResponse? SelectedUser
    {
        get => _selectedUser;
        set
        {
            if (SetProperty(ref _selectedUser, value))
                _ = LoadAsync();
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

    public string CountText => Entries.Count == 0 ? "" : Loc.F("Activity_Count", Entries.Count);

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
            if (Users.Count == 0)
            {
                foreach (var user in await _activity.GetUsersAsync(CancellationToken.None))
                    Users.Add(user);
            }

            var from = From is null ? (DateTimeOffset?)null : new DateTimeOffset(From.Value.Date);

            // Дата «по» включительно
            var toExclusive = To is null ? (DateTimeOffset?)null : new DateTimeOffset(To.Value.Date.AddDays(1));

            var entries = await _activity.GetAsync(
                from, toExclusive, SearchText, SelectedUser?.UserId, CancellationToken.None);

            Entries.Clear();

            foreach (var entry in entries)
                Entries.Add(new ActivityEntryItem(entry));

            IsEmpty = Entries.Count == 0;
            ErrorMessage = "";

            OnPropertyChanged(nameof(CountText));
            OnPropertyChanged(nameof(PeriodTitle));
        }
        catch (Exception ex)
        {
            ErrorMessage = Loc.F("Activity_LoadFailed", ex.Message);
        }
        finally
        {
            IsLoading = false;
        }
    }
}
