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
    /// <summary>
    /// Чтение журнала закупок. Сам приход оформляется приёмкой товара
    /// (<see cref="IProductService.ReceiveAsync"/>) — она заодно заводит новые
    /// карточки и обновляет цены, поэтому второго пути записи прихода здесь нет.
    /// </summary>
    public class PurchaseService : IPurchaseService
    {
        private readonly IDataContext _db;

        public PurchaseService(IDataContext db)
        {
            _db = db;
        }

        public async Task<List<PurchaseListItemResponse>> GetPurchasesAsync(
            DateTimeOffset? from,
            DateTimeOffset? toExclusive,
            string? search,
            CancellationToken ct)
        {
            var purchases = await _db.Purchases
                .AsNoTracking()
                .Select(p => new PurchaseListItemResponse
                {
                    PurchaseId   = p.Id,
                    SupplierName = p.SupplierName,
                    PurchaseDate = p.PurchaseDate,
                    Lines = p.Items
                        .Select(i => new PurchaseLineResponse
                        {
                            ProductId   = i.ProductId,
                            ProductName = i.Product.Name,
                            Barcode     = i.Product.Barcode,
                            Quantity    = i.Quantity,
                            CostPerUnit = i.CostPerUnit,
                        })
                        .ToList(),
                })
                .ToListAsync(ct);

            // SQLite хранит DateTimeOffset текстом и не сравнивает его в SQL,
            // поэтому по датам и поиску отбираем в памяти.
            var result = purchases
                .Where(p => from is null || p.PurchaseDate >= from.Value)
                .Where(p => toExclusive is null || p.PurchaseDate < toExclusive.Value)
                .Where(p => Matches(p, search))
                .OrderByDescending(p => p.PurchaseDate)
                .ToList();

            foreach (var purchase in result)
            {
                purchase.PositionsCount = purchase.Lines.Count;
                purchase.ItemsCount     = purchase.Lines.Sum(l => l.Quantity);
                purchase.TotalCost      = purchase.Lines.Sum(l => l.Total);
            }

            return result;
        }

        public async Task<PurchaseResponse?> GetPurchaseAsync(long id, CancellationToken ct)
        {
            var purchase = await _db.Purchases
                .Include(x => x.Items)
                .FirstOrDefaultAsync(x => x.Id == id, ct);

            return purchase == null ? null : new PurchaseResponse(purchase);
        }

        // Поиск идёт по поставщику и товарам: латиница и кириллица одинаково,
        // потому что сравнение культурное, а не через SQL LIKE
        private static bool Matches(PurchaseListItemResponse purchase, string? search)
        {
            if (string.IsNullOrWhiteSpace(search))
                return true;

            var term = search.Trim();

            return Contains(purchase.SupplierName, term)
                   || purchase.Lines.Any(l => Contains(l.ProductName, term) || Contains(l.Barcode, term));
        }

        private static bool Contains(string? value, string term)
            => value is not null && value.Contains(term, StringComparison.CurrentCultureIgnoreCase);
    }
}
