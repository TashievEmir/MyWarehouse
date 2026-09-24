using Application.DTOs.Labels;

namespace Application.Contracts.Interfaces
{
    /// <summary>
    /// Маркировка товара: выдача штрихкодов тем, кто приехал без них,
    /// и печать этикеток на чековом принтере.
    /// </summary>
    public interface ILabelService
    {
        /// <summary>Товары для маркировки. Фильтр оставляет только те, у которых кода ещё нет.</summary>
        Task<List<LabelProductResponse>> GetProductsAsync(
            string? search,
            bool onlyWithoutBarcode,
            CancellationToken ct);

        /// <summary>
        /// Выдаёт товару следующий свободный внутренний код EAN-13 и сохраняет его.
        /// Возвращает выданный код.
        /// </summary>
        Task<string> GenerateBarcodeAsync(long productId, long userId, CancellationToken ct);

        /// <summary>
        /// Ставит товару код, введённый руками — например, переписанный
        /// с заводской упаковки. Код проверяется на формат и занятость.
        /// </summary>
        Task SetBarcodeAsync(long productId, string barcode, long userId, CancellationToken ct);

        /// <summary>Печатает этикетки. Товар без кода промаркировать нечем.</summary>
        Task PrintLabelsAsync(long productId, LabelOptions options, CancellationToken ct);
    }
}
