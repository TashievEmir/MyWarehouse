using Application.Contracts.Interfaces;
using Application.DTOs.Notifications;
using Microsoft.Extensions.Configuration;

namespace Infrastructure.Notifications
{
    /// <summary>
    /// Читает настройки почты из секции «Notifications» в appsettings.json.
    ///
    /// Файл лежит рядом с exe, поэтому пароль приложения правится без
    /// пересборки — и не хранится в базе, куда попадал бы в открытом виде.
    /// Значение перечитывается на каждом обращении: правку файла подхватит
    /// ближайший проход рассылки, перезапуск не нужен.
    /// </summary>
    public class ConfigNotificationSettingsProvider : INotificationSettingsProvider
    {
        private readonly IConfiguration _configuration;

        public ConfigNotificationSettingsProvider(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public NotificationSettingsResponse Get()
        {
            var section = _configuration.GetSection("Notifications");

            return new NotificationSettingsResponse
            {
                IsEnabled   = Flag(section["Enabled"], false),
                SmtpHost    = (section["SmtpHost"] ?? "").Trim(),
                SmtpPort    = Number(section["SmtpPort"], 587),
                UseSsl      = Flag(section["UseSsl"], true),
                Username    = (section["Username"] ?? "").Trim(),
                // Пробелы режем намеренно: пароль приложения Google показывает
                // группами по четыре, и его почти всегда копируют вместе с ними
                Password    = (section["Password"] ?? "").Replace(" ", ""),
                FromAddress = (section["FromAddress"] ?? "").Trim(),
                FromName    = (section["FromName"] ?? "").Trim(),
                SendTimes   = string.IsNullOrWhiteSpace(section["SendTimes"])
                    ? NotificationSettingsResponse.DefaultTimes
                    : section["SendTimes"]!.Trim(),
            };
        }

        // Значения читаем вручную: типизированный GetValue живёт в отдельном
        // пакете Configuration.Binder, тянуть его ради двух полей незачем.
        // Опечатка в файле не должна ронять приложение — берём значение по умолчанию.
        private static bool Flag(string? raw, bool fallback)
            => bool.TryParse(raw, out var value) ? value : fallback;

        private static int Number(string? raw, int fallback)
            => int.TryParse(raw, out var value) ? value : fallback;
    }
}
