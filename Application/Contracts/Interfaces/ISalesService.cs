using Application.DTOs.Sales;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Contracts.Interfaces
{
    public interface ISalesService
    {
        Task<long> CreateSaleAsync(CreateSaleRequest request, CancellationToken ct);

        /// <summary>
        /// Чеки за период [from; toExclusive). Поиск — по номеру чека, кассиру,
        /// клиенту и товарам внутри чека.
        /// </summary>
        Task<List<ReceiptListItemResponse>> GetReceiptsAsync(
            DateTimeOffset? from,
            DateTimeOffset? toExclusive,
            string? search,
            CancellationToken ct);

        /// <summary>Чек целиком: позиции и итоги.</summary>
        Task<ReceiptDetailsResponse?> GetReceiptAsync(long saleId, CancellationToken ct);

        /// <summary>Незакрытые долги клиентов: продажи, оплаченные не полностью.</summary>
        Task<List<DebtResponse>> GetDebtsAsync(string? search, CancellationToken ct);

        /// <summary>Погашение долга целиком или частично.</summary>
        Task RegisterDebtPaymentAsync(long saleId, decimal amount, long userId, CancellationToken ct);

        Task<SaleResponse?> GetSaleAsync(long saleId, CancellationToken ct);

        Task<List<SaleResponse>> GetSalesByPeriodAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct);
    }
}
