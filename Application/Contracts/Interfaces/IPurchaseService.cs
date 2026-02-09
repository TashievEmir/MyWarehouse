using Application.DTOs.Purchases;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Contracts.Interfaces
{
    public interface IPurchaseService
    {
        Task<long> RegisterPurchaseAsync(RegisterPurchaseRequest request, CancellationToken ct);
        Task<PurchaseResponse?> GetPurchaseAsync(long id, CancellationToken ct);
    }
}
