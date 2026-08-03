using Domain.Exceptions;

namespace Domain.Entities
{
    /// <summary>
    /// Отметка об отправке напоминания за один слот рассылки. Нужна, чтобы после
    /// перезапуска приложения клиент не получил письмо за тот же слот второй раз.
    ///
    /// Пока попытки не исчерпаны, запись остаётся открытой и рассылка вернётся
    /// к ней на следующем проходе — так переживается короткая пропажа интернета.
    /// </summary>
    public class DebtReminder
    {
        /// <summary>Сколько раз пробуем отправить письмо за один слот.</summary>
        public const int MaxAttempts = 3;

        public long Id { get; private set; }

        public long SaleId { get; private set; }

        /// <summary>Ключ вида «2026-08-05#1»: дата рассылки и номер слота за день.</summary>
        public string SlotKey { get; private set; } = "";

        /// <summary>Время последней попытки.</summary>
        public DateTimeOffset SentAt { get; private set; }

        public string Recipient { get; private set; } = "";

        /// <summary>Сумма долга на момент письма — видно, о чём напоминали.</summary>
        public decimal Amount { get; private set; }

        public bool IsSuccess { get; private set; }

        /// <summary>
        /// Слот закрыт без письма: его время прошло, пока приложение было выключено.
        /// Досылаем только последний пропущенный, иначе клиент получит пачку
        /// одинаковых писем разом.
        /// </summary>
        public bool IsSkipped { get; private set; }

        /// <summary>Сколько раз пытались отправить.</summary>
        public int Attempts { get; private set; }

        /// <summary>Текст последней ошибки, если письмо не ушло.</summary>
        public string? Error { get; private set; }

        /// <summary>
        /// Слот ещё в работе: письмо не ушло, но попытки не исчерпаны.
        /// Следующий проход попробует снова.
        /// </summary>
        public bool IsPending => !IsSuccess && !IsSkipped && Attempts < MaxAttempts;

        private DebtReminder() { }

        public DebtReminder(long saleId, string slotKey, string recipient, decimal amount)
        {
            if (saleId <= 0)
                throw new DomainException("Sale is required");

            if (string.IsNullOrWhiteSpace(slotKey))
                throw new DomainException("Slot key is required");

            SaleId = saleId;
            SlotKey = slotKey;
            Recipient = recipient ?? "";
            Amount = amount;
            SentAt = DateTimeOffset.UtcNow;
        }

        public void MarkSent()
        {
            IsSuccess = true;
            IsSkipped = false;
            Error = null;
            Attempts++;
            SentAt = DateTimeOffset.UtcNow;
        }

        public void RegisterFailure(string? error)
        {
            IsSuccess = false;
            Error = error;
            Attempts++;
            SentAt = DateTimeOffset.UtcNow;
        }

        /// <summary>Слот закрываем без письма: его время прошло или подоспел более свежий.</summary>
        public void MarkSkipped()
        {
            IsSkipped = true;
            IsSuccess = false;
            Error = null;
            SentAt = DateTimeOffset.UtcNow;
        }

        /// <summary>Сумма долга могла измениться между попытками — письмо шлём с актуальной.</summary>
        public void UpdateAmount(decimal amount) => Amount = amount;
    }
}
