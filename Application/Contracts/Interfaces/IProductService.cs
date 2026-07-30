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

        /// <summary>Каталог товаров с остатками. Пустой поиск — весь список.</summary>
        Task<List<ProductListItemResponse>> GetCatalogAsync(string? search, CancellationToken ct);

        /// <summary>Правит карточку товара: название, категорию, коды, цены, описание.</summary>
        Task UpdateAsync(UpdateProductRequest request, CancellationToken ct);

        /// <summary>Списывает количество со склада, сохраняя причину и автора.</summary>
        Task WriteOffAsync(WriteOffProductRequest request, CancellationToken ct);

        /// <summary>
        /// Удаляет карточку товара. Товар с продажами, поставками или списаниями
        /// удалить нельзя — вместе с ним пропала бы история.
        /// </summary>
        Task DeleteAsync(long productId, CancellationToken ct);

        /// <summary>
        /// Приходует партию отсканированных товаров: заводит новые товары,
        /// увеличивает остатки и записывает покупку. Возвращает Id покупки.
        /// </summary>
        Task<long> ReceiveAsync(ReceiveProductsRequest request, CancellationToken ct);
    }
}
