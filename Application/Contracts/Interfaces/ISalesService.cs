using Application.DTOs.Sales;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Contracts.Interfaces
{
    public interface ISalesService
    {
        Task<long> CreateSaleAsync(CreateSaleRequest request, CancellationToken ct);

        Task RegisterDebtPaymentAsync(long saleId, decimal amount, long userId, CancellationToken ct);

        Task<SaleResponse?> GetSaleAsync(long saleId, CancellationToken ct);

        Task<List<SaleResponse>> GetSalesByPeriodAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct);
    }
}
