using Application.Contracts.Interfaces;
using Application.Contracts.Persistence;
using Application.DTOs.Sales;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Services
{
    public class SalesService : ISalesService
    {
        private readonly IDataContext _db;

        public SalesService(IDataContext db)
        {
            _db = db;
        }

        public async Task<long> CreateSaleAsync(CreateSaleRequest request, CancellationToken ct)
        {
            await using var tx = await _db.BeginTransactionAsync(ct);

            try
            {
                var sale = new Sale(request.CustomerId, request.UserId);

                foreach (var item in request.Items)
                {
                    var inventory = await _db.Inventories
                        .FirstAsync(x => x.ProductId == item.ProductId, ct);

                    inventory.Decrease(item.Quantity);

                    sale.AddItem(item.ProductId, item.Quantity, item.Price);
                }

                sale.Pay(request.PaidAmount);

                _db.Sales.Add(sale);
                await _db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);

                return sale.Id;
            }
            catch
            {
                await tx.RollbackAsync(ct);
                throw;
            }
            
        }

        public async Task<SaleResponse?> GetSaleAsync(long saleId, CancellationToken ct)
        {
            return await _db.Sales
            .Where(x => x.Id == saleId)
            .Select(x => new SaleResponse(x))
            .FirstOrDefaultAsync(ct);
        }

        public async Task<List<SaleResponse>> GetSalesByPeriodAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct)
        {
            return await _db.Sales
            .Where(x => x.SaleDate >= from && x.SaleDate <= to)
            .Select(x => new SaleResponse(x))
            .ToListAsync(ct);
        }

        public async Task RegisterDebtPaymentAsync(long saleId, decimal amount, long userId, CancellationToken ct)
        {
            var sale = await _db.Sales.FindAsync([saleId], ct)
                   ?? throw new Exception("Sale not found");

            sale.Pay(amount);

            _db.DebtPayments.Add(new DebtPayment(saleId, userId, amount));

            await _db.SaveChangesAsync(ct);
        }
    }
}
