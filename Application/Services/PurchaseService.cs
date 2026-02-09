using Application.Contracts.Interfaces;
using Application.Contracts.Persistence;
using Application.DTOs.Purchases;
using Domain.Entities;
using Domain.Exceptions;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Services
{
    public class PurchaseService : IPurchaseService
    {
        private readonly IDataContext _db;

        public PurchaseService(IDataContext db)
        {
            _db = db;
        }

        public async Task<long> RegisterPurchaseAsync(RegisterPurchaseRequest request, CancellationToken ct)
        {
            var purchase = new Purchase(request.SupplierName);

            foreach (var item in request.Items)
            {
                var inventory = await _db.Inventories
                    .FirstOrDefaultAsync(x => x.ProductId == item.ProductId, ct)
                    ?? throw new DomainException("Inventory not found");

                inventory.Increase(item.Quantity);

                purchase.AddItem(item.ProductId, item.Quantity, item.Cost);
            }

            _db.Purchases.Add(purchase);
            await _db.SaveChangesAsync(ct);

            return purchase.Id;
        }

        public async Task<PurchaseResponse?> GetPurchaseAsync(long id, CancellationToken ct)
        {
            var purchase = await _db.Purchases
                .Include(x => x.Items)
                .FirstOrDefaultAsync(x => x.Id == id, ct);

            return purchase == null ? null : new PurchaseResponse(purchase);
        }
    }
}
