using Application.DTOs.Notifications;

namespace Application.Contracts.Interfaces
{
    /// <summary>Отправка письма. Реализация живёт в Infrastructure — там же SMTP.</summary>
    public interface IEmailSender
    {
        Task SendAsync(NotificationSettingsResponse settings, EmailMessage message, CancellationToken ct);
    }

    public interface INotificationSettingsService
    {
        Task<NotificationSettingsResponse> GetAsync(CancellationToken ct);

        Task SaveAsync(SaveNotificationSettingsRequest request, CancellationToken ct);
    }

    public interface IDebtReminderService
    {
        /// <summary>
        /// Рассылает напоминания по долгам, у которых наступил срок. За каждый слот
        /// дня письмо уходит один раз — повторный вызов ничего не продублирует.
        /// </summary>
        Task<ReminderRunResult> RunAsync(CancellationToken ct);

        /// <summary>Отправляет пробное письмо на указанный адрес — проверка настроек.</summary>
        Task SendTestAsync(string recipient, CancellationToken ct);
    }
}
