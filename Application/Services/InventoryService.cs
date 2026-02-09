using Application.Contracts.Interfaces;
using Application.Contracts.Persistence;
using Application.DTOs.Inventories;
using Domain.Exceptions;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Services
{
    public class InventoryService : IInventoryService
    {
        private readonly IDataContext _db;

        public InventoryService(IDataContext db)
        {
            _db = db;
        }

        public async Task<InventoryResponse?> GetInventoryAsync(long productId, CancellationToken ct)
        {
            var inv = await _db.Inventories
                .FirstOrDefaultAsync(x => x.ProductId == productId, ct);

            return inv == null ? null : new InventoryResponse(inv);
        }

        public async Task<List<InventoryResponse>> GetAllAsync(CancellationToken ct)
        {
            return await _db.Inventories
                .Select(x => new InventoryResponse(x))
                .ToListAsync(ct);
        }

        public async Task AdjustInventoryAsync(AdjustInventoryRequest request, CancellationToken ct)
        {
            var inv = await _db.Inventories
                .FirstOrDefaultAsync(x => x.ProductId == request.ProductId, ct)
                ?? throw new DomainException("Inventory not found");

            if (request.Delta > 0)
                inv.Increase(request.Delta);
            else
                inv.Decrease(Math.Abs(request.Delta));

            await _db.SaveChangesAsync(ct);
        }
    }
}
