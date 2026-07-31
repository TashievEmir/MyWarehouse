using Application.DTOs.Purchases;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Contracts.Interfaces
{
    public interface IPurchaseService
    {
        /// <summary>
        /// Журнал закупок за период [from; toExclusive) вместе с составом поставок.
        /// Границы необязательны, поиск — по поставщику и товарам.
        /// </summary>
        Task<List<PurchaseListItemResponse>> GetPurchasesAsync(
            DateTimeOffset? from,
            DateTimeOffset? toExclusive,
            string? search,
            CancellationToken ct);

        Task<PurchaseResponse?> GetPurchaseAsync(long id, CancellationToken ct);
    }
}
