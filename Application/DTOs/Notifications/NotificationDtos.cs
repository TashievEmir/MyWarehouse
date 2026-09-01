namespace Application.DTOs.Notifications
{
    public class NotificationSettingsResponse
    {
        /// <summary>Рассылка раз в день, в обед — по умолчанию.</summary>
        public const string DefaultTimes = "14:00";

        public bool IsEnabled { get; set; }

        public string SmtpHost { get; set; } = "";
        public int SmtpPort { get; set; } = 587;
        public bool UseSsl { get; set; } = true;

        public string Username { get; set; } = "";
        public string Password { get; set; } = "";

        public string FromAddress { get; set; } = "";
        public string FromName { get; set; } = "";

        public string SendTimes { get; set; } = DefaultTimes;

        /// <summary>Почта настроена настолько, чтобы пытаться отправлять.</summary>
        public bool IsConfigured => SmtpHost.Length > 0 && FromAddress.Length > 0;
    }

    /// <summary>Одно письмо.</summary>
    public class EmailMessage
    {
        public string To { get; set; } = "";
        public string Subject { get; set; } = "";
        public string Body { get; set; } = "";
    }

    /// <summary>Итог прохода рассылки — показывается на странице напоминаний.</summary>
    public class ReminderRunResult
    {
        public int Sent { get; set; }
        public int Failed { get; set; }

        /// <summary>Долги, у которых наступил срок, но у клиента нет почты.</summary>
        public int WithoutEmail { get; set; }

        /// <summary>Слоты, чьё время прошло при выключенном приложении.</summary>
        public int Skipped { get; set; }

        /// <summary>Отправка сорвалась, но попытки не исчерпаны — повторим на следующем проходе.</summary>
        public int Retrying { get; set; }

        public string? Error { get; set; }

        public bool DidNothing => Sent == 0 && Failed == 0;
    }
}
