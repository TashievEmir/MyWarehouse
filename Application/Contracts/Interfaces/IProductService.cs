using Application.DTOs.Products;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Contracts.Interfaces
{
    public interface IProductService
    {
        /// <summary>
        /// Остатки по категориям: сколько товара есть сейчас и сколько поступило
        /// за период [from; toExclusive). Границы необязательны.
        /// </summary>
        Task<List<CategoryStockResponse>> GetStockByCategoryAsync(
            DateTimeOffset? from,
            DateTimeOffset? toExclusive,
            CancellationToken ct);

        /// <summary>Поиск товара по штрихкоду. null, если такого товара ещё нет.</summary>
        Task<ProductLookupResponse?> FindByBarcodeAsync(string barcode, CancellationToken ct);

        /// <summary>
        /// Приходует партию отсканированных товаров: заводит новые товары,
        /// увеличивает остатки и записывает покупку. Возвращает Id покупки.
        /// </summary>
        Task<long> ReceiveAsync(ReceiveProductsRequest request, CancellationToken ct);
    }
}
