namespace Application.Contracts.Interfaces
{
    /// <summary>
    /// Печать чеков: собирает ленту из шаблона и данных продажи и отправляет
    /// её на принтер.
    /// </summary>
    public interface IReceiptPrintService
    {
        /// <summary>Печатать ли чек сразу после закрытия сделки.</summary>
        Task<bool> IsAutoPrintEnabledAsync(CancellationToken ct);

        /// <summary>
        /// Печатает чек по продаже. Наличные и сдачу знает только касса —
        /// в сохранённом чеке их нет, поэтому они приходят отдельно.
        /// Ящик открывается при оплате наличными и включённой настройке.
        /// </summary>
        Task PrintSaleAsync(long saleId, decimal? cashGiven, decimal? change, CancellationToken ct);

        /// <summary>Тестовый чек с образцами позиций — проверить принтер после настройки.</summary>
        Task PrintTestAsync(CancellationToken ct);
    }
}
