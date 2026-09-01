using Application.DTOs.Notifications;
using Application.DTOs.Sales;

namespace Application.Contracts.Interfaces
{
    /// <summary>Отправка письма. Реализация живёт в Infrastructure — там же SMTP.</summary>
    public interface IEmailSender
    {
        Task SendAsync(NotificationSettingsResponse settings, EmailMessage message, CancellationToken ct);
    }

    /// <summary>
    /// Настройки почты. Раньше лежали в базе и правились из интерфейса, теперь
    /// приходят из appsettings.json: пароль приложения — секрет окружения,
    /// а не пользовательская настройка.
    /// </summary>
    public interface INotificationSettingsProvider
    {
        NotificationSettingsResponse Get();
    }

    public interface IDebtReminderService
    {
        /// <summary>
        /// Рассылает напоминания по долгам, у которых наступил срок. За каждый слот
        /// дня письмо уходит один раз — повторный вызов ничего не продублирует.
        /// </summary>
        Task<ReminderRunResult> RunAsync(CancellationToken ct);

        /// <summary>
        /// Досрочное напоминание конкретному должнику: письмо уходит сразу,
        /// не дожидаясь срока оплаты и слота рассылки.
        /// </summary>
        Task SendToDebtorAsync(long saleId, CancellationToken ct);

        /// <summary>Долги с почтой — список для досрочной отправки.</summary>
        Task<List<DebtResponse>> GetReachableDebtorsAsync(CancellationToken ct);
    }
}
