using System.Collections.ObjectModel;
using System.Windows.Input;
using Application.Contracts.Interfaces;
using Application.Contracts.Persistence;
using Application.DTOs.Notifications;
using Microsoft.EntityFrameworkCore;
using Wpf.Common;
using Wpf.Localization;
using Wpf.Services;

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

    public ReminderLogItem(
        DateTimeOffset sentAt, string recipient, decimal amount,
        bool isSuccess, bool isSkipped, int attempts, string? error)
    {
        SentAtText = sentAt.ToLocalTime().ToString("dd.MM.yyyy HH:mm", Loc.Instance.Culture);
        Recipient = recipient;
        AmountText = amount.ToString("N2", Loc.Instance.Culture);
        IsSuccess = isSuccess;
        IsSkipped = isSkipped;

        if (isSkipped)
        {
            StatusText = Loc.T("Notif_LogSkipped");
        }
        else if (isSuccess)
        {
            StatusText = Loc.T("Notif_LogSent");
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

/// <summary>
/// Настройки почтовых напоминаний о долге и журнал последних отправок.
/// </summary>
public class NotificationsViewModel : ViewModelBase
{
    private const int LogSize = 20;

    private readonly INotificationSettingsService _settings;
    private readonly IDebtReminderService _reminders;
    private readonly IDataContext _db;
    private readonly SessionService _session;

    public ObservableCollection<ReminderLogItem> Log { get; } = new();

    public ICommand SaveCommand { get; }
    public ICommand TestCommand { get; }
    public ICommand RunNowCommand { get; }

    public NotificationsViewModel(
        INotificationSettingsService settings,
        IDebtReminderService reminders,
        IDataContext db,
        SessionService session)
    {
        _settings = settings;
        _reminders = reminders;
        _db = db;
        _session = session;

        SaveCommand = new AsyncRelayCommand(SaveAsync);
        TestCommand = new AsyncRelayCommand(SendTestAsync);
        RunNowCommand = new AsyncRelayCommand(RunNowAsync);

        Loc.LanguageChanged += () => OnPropertyChanged(string.Empty);
    }

    // ===================== Поля =====================

    private bool _isEnabled;
    public bool IsEnabled
    {
        get => _isEnabled;
        set => SetProperty(ref _isEnabled, value);
    }

    private string _smtpHost = "";
    public string SmtpHost
    {
        get => _smtpHost;
        set => SetProperty(ref _smtpHost, value);
    }

    private int _smtpPort = 587;
    public int SmtpPort
    {
        get => _smtpPort;
        set => SetProperty(ref _smtpPort, value);
    }

    private bool _useSsl = true;
    public bool UseSsl
    {
        get => _useSsl;
        set => SetProperty(ref _useSsl, value);
    }

    private string _username = "";
    public string Username
    {
        get => _username;
        set => SetProperty(ref _username, value);
    }

    private string _password = "";
    public string Password
    {
        get => _password;
        set => SetProperty(ref _password, value);
    }

    private string _fromAddress = "";
    public string FromAddress
    {
        get => _fromAddress;
        set => SetProperty(ref _fromAddress, value);
    }

    private string _fromName = "";
    public string FromName
    {
        get => _fromName;
        set => SetProperty(ref _fromName, value);
    }

    private string _sendTimes = Domain.Entities.NotificationSettings.DefaultTimes;
    public string SendTimes
    {
        get => _sendTimes;
        set => SetProperty(ref _sendTimes, value);
    }

    private string _testRecipient = "";
    public string TestRecipient
    {
        get => _testRecipient;
        set => SetProperty(ref _testRecipient, value);
    }

    // ===================== Состояние =====================

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
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

    // ===================== Загрузка и сохранение =====================

    public async Task LoadAsync()
    {
        try
        {
            var settings = await _settings.GetAsync(CancellationToken.None);

            IsEnabled = settings.IsEnabled;
            SmtpHost = settings.SmtpHost;
            SmtpPort = settings.SmtpPort;
            UseSsl = settings.UseSsl;
            Username = settings.Username;
            Password = settings.Password;
            FromAddress = settings.FromAddress;
            FromName = settings.FromName;
            SendTimes = settings.SendTimes;

            if (TestRecipient.Length == 0)
                TestRecipient = settings.FromAddress;

            await LoadLogAsync();
        }
        catch (Exception ex)
        {
            ShowError(Loc.F("Notif_LoadFailed", ex.Message));
        }
    }

    private async Task LoadLogAsync()
    {
        // Сортировка в памяти: SQLite не умеет упорядочивать DateTimeOffset
        var rows = await _db.DebtReminders
            .AsNoTracking()
            .Select(r => new { r.SentAt, r.Recipient, r.Amount, r.IsSuccess, r.IsSkipped, r.Attempts, r.Error })
            .ToListAsync();

        Log.Clear();

        foreach (var row in rows.OrderByDescending(r => r.SentAt).Take(LogSize))
            Log.Add(new ReminderLogItem(
                row.SentAt, row.Recipient, row.Amount, row.IsSuccess, row.IsSkipped, row.Attempts, row.Error));

        OnPropertyChanged(nameof(IsLogEmpty));
    }

    private async Task SaveAsync()
    {
        if (_session.User is null)
        {
            ShowError(Loc.T("Notif_NoLogin"));
            return;
        }

        IsBusy = true;

        try
        {
            await _settings.SaveAsync(new SaveNotificationSettingsRequest
            {
                UserId = _session.User.UserId,
                IsEnabled = IsEnabled,
                SmtpHost = SmtpHost,
                SmtpPort = SmtpPort,
                UseSsl = UseSsl,
                Username = Username,
                Password = Password,
                FromAddress = FromAddress,
                FromName = FromName,
                SendTimes = SendTimes,
            }, CancellationToken.None);

            await LoadAsync();

            ShowInfo(Loc.T("Notif_Saved"));
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

    private async Task SendTestAsync()
    {
        IsBusy = true;

        try
        {
            await _reminders.SendTestAsync(TestRecipient, CancellationToken.None);

            ShowInfo(Loc.F("Notif_TestSent", TestRecipient.Trim()));
        }
        catch (Exception ex)
        {
            ShowError(Loc.F("Notif_TestFailed", ex.Message));
        }
        finally
        {
            IsBusy = false;
        }
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
