using Application.DTOs.Inventories;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Contracts.Interfaces
{
    public interface IInventoryService
    {
        Task<InventoryResponse?> GetInventoryAsync(long productId, CancellationToken ct);

        Task AdjustInventoryAsync(AdjustInventoryRequest request, CancellationToken ct);

        Task<List<InventoryResponse>> GetAllAsync(CancellationToken ct);
    }
}
