using Domain.Exceptions;

namespace Domain.Entities
{
    /// <summary>
    /// Настройки почтовых напоминаний о долге. В базе одна строка — как у шаблона чека.
    /// </summary>
    public class NotificationSettings
    {
        /// <summary>Времена рассылки по умолчанию: утро, день, вечер.</summary>
        public const string DefaultTimes = "10:00,14:00,18:00";

        public long Id { get; private set; }

        /// <summary>Выключено — приложение ничего не отправляет.</summary>
        public bool IsEnabled { get; private set; }

        public string SmtpHost { get; private set; } = "";
        public int SmtpPort { get; private set; } = 587;
        public bool UseSsl { get; private set; } = true;

        public string Username { get; private set; } = "";
        public string Password { get; private set; } = "";

        public string FromAddress { get; private set; } = "";
        public string FromName { get; private set; } = "";

        /// <summary>Времена рассылки через запятую, например «10:00,14:00,18:00».</summary>
        public string SendTimes { get; private set; } = DefaultTimes;

        private NotificationSettings() { }

        public NotificationSettings(
            bool isEnabled,
            string smtpHost,
            int smtpPort,
            bool useSsl,
            string username,
            string password,
            string fromAddress,
            string fromName,
            string sendTimes)
        {
            Update(isEnabled, smtpHost, smtpPort, useSsl, username, password, fromAddress, fromName, sendTimes);
        }

        public void Update(
            bool isEnabled,
            string smtpHost,
            int smtpPort,
            bool useSsl,
            string username,
            string password,
            string fromAddress,
            string fromName,
            string sendTimes)
        {
            // Проверяем только при включённой рассылке: выключенную можно хранить недозаполненной
            if (isEnabled)
            {
                if (string.IsNullOrWhiteSpace(smtpHost))
                    throw new DomainException("SMTP host is required");

                if (smtpPort is <= 0 or > 65535)
                    throw new DomainException("SMTP port is out of range");

                if (string.IsNullOrWhiteSpace(fromAddress))
                    throw new DomainException("Sender address is required");
            }

            IsEnabled = isEnabled;
            SmtpHost = (smtpHost ?? "").Trim();
            SmtpPort = smtpPort;
            UseSsl = useSsl;
            Username = (username ?? "").Trim();
            Password = password ?? "";
            FromAddress = (fromAddress ?? "").Trim();
            FromName = (fromName ?? "").Trim();
            SendTimes = string.IsNullOrWhiteSpace(sendTimes) ? DefaultTimes : sendTimes.Trim();
        }
    }
}
