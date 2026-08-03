using System.Net;
using System.Net.Mail;
using Application.Contracts.Interfaces;
using Application.DTOs.Notifications;

namespace Infrastructure.Notifications
{
    /// <summary>
    /// Отправка через обычный SMTP. Для Gmail и Яндекса нужен пароль приложения,
    /// обычный пароль от аккаунта они не принимают.
    /// </summary>
    public class SmtpEmailSender : IEmailSender
    {
        public async Task SendAsync(NotificationSettingsResponse settings, EmailMessage message, CancellationToken ct)
        {
            using var client = new SmtpClient(settings.SmtpHost, settings.SmtpPort)
            {
                EnableSsl = settings.UseSsl,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                Timeout = 30_000,
            };

            // Пустой логин — сервер без авторизации, такое бывает во внутренних сетях
            if (settings.Username.Length > 0)
            {
                client.UseDefaultCredentials = false;
                client.Credentials = new NetworkCredential(settings.Username, settings.Password);
            }

            var from = settings.FromName.Length > 0
                ? new MailAddress(settings.FromAddress, settings.FromName)
                : new MailAddress(settings.FromAddress);

            using var mail = new MailMessage
            {
                From = from,
                Subject = message.Subject,
                Body = message.Body,
                IsBodyHtml = false,
            };

            mail.To.Add(message.To);

            await client.SendMailAsync(mail, ct);
        }
    }
}
