using Application.DTOs.Printing;

namespace Application.Contracts.Interfaces
{
    /// <summary>
    /// Транспорт до железа: перевод готового чека в команды принтера.
    /// Реализация живёт в Infrastructure, слой Application о драйверах не знает.
    /// </summary>
    public interface IReceiptPrinter
    {
        /// <summary>Принтеры, установленные в системе — для выпадающего списка в настройках.</summary>
        IReadOnlyList<string> GetInstalledPrinters();

        /// <summary>Принтер Windows по умолчанию; null — принтеров в системе нет.</summary>
        string? GetDefaultPrinter();

        /// <summary>Печатает чек. Бросает исключение, если принтер недоступен.</summary>
        Task PrintAsync(ReceiptDocument document, CancellationToken ct);
    }
}
