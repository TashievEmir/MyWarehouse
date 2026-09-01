using System.Collections.ObjectModel;
using System.Windows.Input;
using Application.Contracts.Interfaces;
using Application.Contracts.Persistence;
using Application.DTOs.Sales;
using Microsoft.EntityFrameworkCore;
using Wpf.Common;
using Wpf.Localization;

namespace Wpf.ViewModels.Notifications;

/// <summary>Строка журнала рассылки.</summary>
public class ReminderLogItem
{
    public string SentAtText { get; }
    public string Recipient { get; }
    public string AmountText { get; }
    public bool IsSuccess { get; }
    public string StatusText { get; }

    public bool IsSkipped { get; }

    /// <summary>Досрочная отправка — её видно по ключу слота.</summary>
    public bool IsManual { get; }

    public ReminderLogItem(
        DateTimeOffset sentAt, string recipient, decimal amount,
        bool isSuccess, bool isSkipped, int attempts, string? error, string slotKey)
    {
        SentAtText = sentAt.ToLocalTime().ToString("dd.MM.yyyy HH:mm", Loc.Instance.Culture);
        Recipient = recipient;
        AmountText = amount.ToString("N2", Loc.Instance.Culture);
        IsSuccess = isSuccess;
        IsSkipped = isSkipped;
        IsManual = slotKey.Contains("#manual");

        if (isSkipped)
        {
            StatusText = Loc.T("Notif_LogSkipped");
        }
        else if (isSuccess)
        {
            StatusText = IsManual ? Loc.T("Notif_LogSentManual") : Loc.T("Notif_LogSent");
        }
        else
        {
            // Видно, добьёт ли рассылка этот слот сама или уже сдалась
            var left = attempts < Domain.Entities.DebtReminder.MaxAttempts
                ? Loc.F("Notif_LogRetry", attempts, Domain.Entities.DebtReminder.MaxAttempts)
                : Loc.F("Notif_LogGaveUp", attempts);

            StatusText = $"{left} · {error}";
        }
    }
}

/// <summary>Должник в списке досрочной отправки.</summary>
public class DebtorItem
{
    public long SaleId { get; }
    public string Title { get; }

    public DebtorItem(DebtResponse debt)
    {
        SaleId = debt.SaleId;

        Title = Loc.F(
            "Notif_DebtorLine",
            debt.CustomerName,
            debt.Debt.ToString("N2", Loc.Instance.Culture),
            debt.CustomerEmail ?? "");
    }
}

/// <summary>
/// Журнал почтовых напоминаний и досрочная отправка.
///
/// Настройки SMTP сюда больше не заводятся — они лежат в appsettings.json
/// рядом с exe. Страница только показывает, что настроено, и даёт отправить
/// напоминание конкретному должнику, не дожидаясь расписания.
/// </summary>
public class NotificationsViewModel : ViewModelBase
{
    private const int LogSize = 50;

    private readonly IDebtReminderService _reminders;
    private readonly INotificationSettingsProvider _settings;
    private readonly IDataContext _db;

    public ObservableCollection<ReminderLogItem> Log { get; } = new();

    public ObservableCollection<DebtorItem> Debtors { get; } = new();

    public ICommand RefreshCommand { get; }
    public ICommand RunNowCommand { get; }
    public ICommand SendToDebtorCommand { get; }

    public NotificationsViewModel(
        IDebtReminderService reminders,
        INotificationSettingsProvider settings,
        IDataContext db)
    {
        _reminders = reminders;
        _settings = settings;
        _db = db;

        RefreshCommand = new AsyncRelayCommand(LoadAsync);
        RunNowCommand = new AsyncRelayCommand(RunNowAsync);
        SendToDebtorCommand = new AsyncRelayCommand(SendToDebtorAsync);

        Loc.LanguageChanged += () => OnPropertyChanged(string.Empty);
    }

    // ===================== Что настроено =====================

    /// <summary>Читается из appsettings при каждом открытии страницы.</summary>
    public bool IsMailEnabled => _settings.Get().IsEnabled;

    public bool IsMailConfigured => _settings.Get().IsConfigured;

    /// <summary>Настройки неполные или рассылка выключена — предупреждаем.</summary>
    public bool ShowMailWarning => !IsMailEnabled || !IsMailConfigured;

    public string MailWarningText => !IsMailConfigured
        ? Loc.T("Notif_NotConfigured")
        : Loc.T("Notif_Disabled");

    public string MailServerText
    {
        get
        {
            var s = _settings.Get();

            return s.SmtpHost.Length == 0
                ? Loc.T("Notif_ServerNotSet")
                : $"{s.SmtpHost}:{s.SmtpPort}{(s.UseSsl ? " · SSL/TLS" : "")}";
        }
    }

    public string MailFromText
    {
        get
        {
            var s = _settings.Get();

            return s.FromAddress.Length == 0 ? Loc.T("Notif_FromNotSet") : s.FromAddress;
        }
    }

    public string ScheduleText => Loc.F("Notif_ScheduleAt", _settings.Get().SendTimes);

    private void RaiseSettings()
    {
        OnPropertyChanged(nameof(IsMailEnabled));
        OnPropertyChanged(nameof(IsMailConfigured));
        OnPropertyChanged(nameof(ShowMailWarning));
        OnPropertyChanged(nameof(MailWarningText));
        OnPropertyChanged(nameof(MailServerText));
        OnPropertyChanged(nameof(MailFromText));
        OnPropertyChanged(nameof(ScheduleText));
    }

    // ===================== Досрочная отправка =====================

    private DebtorItem? _selectedDebtor;
    public DebtorItem? SelectedDebtor
    {
        get => _selectedDebtor;
        set
        {
            if (SetProperty(ref _selectedDebtor, value))
                OnPropertyChanged(nameof(CanSendToDebtor));
        }
    }

    public bool CanSendToDebtor => !IsBusy && SelectedDebtor is not null;

    public bool HasDebtors => Debtors.Count > 0;

    private async Task SendToDebtorAsync()
    {
        if (SelectedDebtor is null)
        {
            ShowError(Loc.T("Notif_PickDebtor"));
            return;
        }

        IsBusy = true;

        try
        {
            var debtor = SelectedDebtor;

            await _reminders.SendToDebtorAsync(debtor.SaleId, CancellationToken.None);

            await LoadLogAsync();

            ShowInfo(Loc.F("Notif_SentTo", debtor.Title));
        }
        catch (Exception ex)
        {
            // Неудачная попытка уже записана в журнал — показываем и её
            await LoadLogAsync();

            ShowError(Loc.F("Notif_SendFailed", ex.Message));
        }
        finally
        {
            IsBusy = false;
        }
    }

    // ===================== Состояние =====================

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
                OnPropertyChanged(nameof(CanSendToDebtor));
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

    private bool _statusIsError;
    public bool StatusIsError
    {
        get => _statusIsError;
        private set => SetProperty(ref _statusIsError, value);
    }

    public bool HasStatus => StatusMessage.Length > 0;

    public bool IsLogEmpty => Log.Count == 0;

    // ===================== Загрузка =====================

    public async Task LoadAsync()
    {
        try
        {
            RaiseSettings();

            await LoadDebtorsAsync();
            await LoadLogAsync();
        }
        catch (Exception ex)
        {
            ShowError(Loc.F("Notif_LoadFailed", ex.Message));
        }
    }

    private async Task LoadDebtorsAsync()
    {
        var debtors = await _reminders.GetReachableDebtorsAsync(CancellationToken.None);

        // Выбор кассира переживает обновление списка
        var previous = SelectedDebtor?.SaleId;

        Debtors.Clear();

        foreach (var debt in debtors)
            Debtors.Add(new DebtorItem(debt));

        SelectedDebtor = Debtors.FirstOrDefault(d => d.SaleId == previous) ?? Debtors.FirstOrDefault();

        OnPropertyChanged(nameof(HasDebtors));
    }

    private async Task LoadLogAsync()
    {
        // Сортировка в памяти: SQLite не умеет упорядочивать DateTimeOffset
        var rows = await _db.DebtReminders
            .AsNoTracking()
            .Select(r => new
            {
                r.SentAt, r.Recipient, r.Amount, r.IsSuccess, r.IsSkipped, r.Attempts, r.Error, r.SlotKey
            })
            .ToListAsync();

        Log.Clear();

        foreach (var row in rows.OrderByDescending(r => r.SentAt).Take(LogSize))
            Log.Add(new ReminderLogItem(
                row.SentAt, row.Recipient, row.Amount,
                row.IsSuccess, row.IsSkipped, row.Attempts, row.Error, row.SlotKey));

        OnPropertyChanged(nameof(IsLogEmpty));
    }

    private async Task RunNowAsync()
    {
        IsBusy = true;

        try
        {
            var result = await _reminders.RunAsync(CancellationToken.None);

            await LoadLogAsync();

            var text = Loc.F("Notif_RunResult",
                result.Sent, result.Failed, result.WithoutEmail, result.Skipped, result.Retrying);

            if (result.Failed > 0)
                ShowError(text);
            else
                ShowInfo(text);
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ShowInfo(string message)
    {
        StatusIsError = false;
        StatusMessage = message;
    }

    private void ShowError(string message)
    {
        StatusIsError = true;
        StatusMessage = message;
    }
}
