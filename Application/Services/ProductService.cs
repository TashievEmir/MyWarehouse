using Application.Contracts.Interfaces;
using Application.Contracts.Persistence;
using Application.DTOs.Products;
using Domain.Entities;
using Domain.Exceptions;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Services
{
    public class ProductService : IProductService
    {
        private readonly IDataContext _db;

        public ProductService(IDataContext db)
        {
            _db = db;
        }

        public async Task<List<CategoryStockResponse>> GetStockByCategoryAsync(
            DateTimeOffset? from,
            DateTimeOffset? toExclusive,
            CancellationToken ct)
        {
            var products = await _db.Products
                .AsNoTracking()
                .Select(p => new
                {
                    p.Id,
                    p.Name,
                    p.SKU,
                    p.Barcode,
                    p.CategoryId,
                    CategoryName = p.Category.Name,
                    InStock = p.Inventory != null ? p.Inventory.Quantity : 0
                })
                .ToListAsync(ct);

            var received = await GetReceivedByProductAsync(from, toExclusive, ct);

            var categories = products
                .GroupBy(p => new { p.CategoryId, p.CategoryName })
                .Select(g => new CategoryStockResponse
                {
                    CategoryId = g.Key.CategoryId,
                    Name = g.Key.CategoryName,
                    Products = g
                        .Select(p => new ProductStockResponse
                        {
                            ProductId = p.Id,
                            Name = p.Name,
                            SKU = p.SKU,
                            Barcode = p.Barcode,
                            InStock = p.InStock,
                            Received = received.GetValueOrDefault(p.Id)
                        })
                        // Товары, которых нет и не приходило за период, только зашумляют список
                        .Where(p => p.InStock > 0 || p.Received > 0)
                        .OrderByDescending(p => p.InStock)
                        .ThenBy(p => p.Name)
                        .ToList()
                })
                .Where(c => c.Products.Count > 0)
                .OrderBy(c => c.Name)
                .ToList();

            foreach (var category in categories)
            {
                category.InStock = category.Products.Sum(p => p.InStock);
                category.Received = category.Products.Sum(p => p.Received);
            }

            return categories;
        }

        public async Task<ProductLookupResponse?> FindByBarcodeAsync(string barcode, CancellationToken ct)
        {
            barcode = (barcode ?? "").Trim();

            if (barcode.Length == 0)
                return null;

            return await _db.Products
                .AsNoTracking()
                .Where(p => p.Barcode == barcode)
                .Select(p => new ProductLookupResponse
                {
                    ProductId = p.Id,
                    Name = p.Name,
                    SKU = p.SKU,
                    Barcode = p.Barcode,
                    CategoryId = p.CategoryId,
                    CategoryName = p.Category.Name,
                    InStock = p.Inventory != null ? p.Inventory.Quantity : 0,
                    CostPerUnit = p.CostPerUnit
                })
                .FirstOrDefaultAsync(ct);
        }

        public async Task<long> ReceiveAsync(ReceiveProductsRequest request, CancellationToken ct)
        {
            if (request.Items.Count == 0)
                throw new DomainException("Список товаров пуст");

            await using var tx = await _db.BeginTransactionAsync(ct);

            try
            {
                // Один товар может встретиться в списке несколько раз — в покупке
                // позиция на товар только одна, поэтому количества складываем.
                var lines = new Dictionary<long, (int Quantity, decimal Cost)>();

                foreach (var item in request.Items)
                {
                    if (item.Quantity <= 0)
                        throw new DomainException("Количество должно быть больше нуля");

                    if (item.Cost < 0)
                        throw new DomainException("Цена не может быть отрицательной");

                    var productId = await ResolveProductIdAsync(item, ct);

                    if (lines.TryGetValue(productId, out var line))
                        lines[productId] = (line.Quantity + item.Quantity, line.Cost > 0 ? line.Cost : item.Cost);
                    else
                        lines[productId] = (item.Quantity, item.Cost);
                }

                var supplier = string.IsNullOrWhiteSpace(request.SupplierName)
                    ? "Без поставщика"
                    : request.SupplierName.Trim();

                var purchase = new Purchase(supplier);

                foreach (var (productId, line) in lines)
                {
                    var inventory = await _db.Inventories
                        .FirstOrDefaultAsync(x => x.ProductId == productId, ct);

                    if (inventory is null)
                    {
                        inventory = new Inventory(productId, 0);
                        _db.Inventories.Add(inventory);
                    }

                    inventory.Increase(line.Quantity);

                    purchase.AddItem(productId, line.Quantity, line.Cost);
                }

                _db.Purchases.Add(purchase);

                await _db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);

                return purchase.Id;
            }
            catch
            {
                await tx.RollbackAsync(ct);
                throw;
            }
        }

        /// <summary>
        /// Находит товар позиции или заводит новый — тогда он сразу сохраняется,
        /// чтобы получить Id для остатка и позиции покупки.
        /// </summary>
        private async Task<long> ResolveProductIdAsync(ReceiveItemRequest item, CancellationToken ct)
        {
            var barcode = string.IsNullOrWhiteSpace(item.Barcode) ? null : item.Barcode.Trim();

            if (item.ProductId is > 0)
            {
                var exists = await _db.Products.AnyAsync(p => p.Id == item.ProductId, ct);

                if (!exists)
                    throw new DomainException("Товар не найден");

                return item.ProductId.Value;
            }

            // Товар мог появиться уже после сканирования — не заводим дубль
            if (barcode is not null)
            {
                var existingId = await _db.Products
                    .Where(p => p.Barcode == barcode)
                    .Select(p => p.Id)
                    .FirstOrDefaultAsync(ct);

                if (existingId != 0)
                    return existingId;
            }

            var name = (item.Name ?? "").Trim();

            if (name.Length == 0)
                throw new DomainException($"У нового товара {barcode} не указано название");

            if (item.CategoryId is not > 0)
                throw new DomainException($"У нового товара «{name}» не выбрана категория");

            var categoryExists = await _db.Categories.AnyAsync(c => c.Id == item.CategoryId, ct);

            if (!categoryExists)
                throw new DomainException("Категория не найдена");

            var product = new Product
            {
                Name = name,
                SKU = barcode ?? $"SKU-{Guid.NewGuid():N}"[..12],
                Barcode = barcode,
                CategoryId = item.CategoryId.Value,
                PricePerUnit = 0,
                CostPerUnit = item.Cost,
                CreatedAt = DateTimeOffset.UtcNow
            };

            _db.Products.Add(product);
            await _db.SaveChangesAsync(ct);

            return product.Id;
        }

        private async Task<Dictionary<long, int>> GetReceivedByProductAsync(
            DateTimeOffset? from,
            DateTimeOffset? toExclusive,
            CancellationToken ct)
        {
            // SQLite хранит DateTimeOffset текстом и не умеет сравнивать его в SQL,
            // поэтому по датам отбираем уже в памяти — тянем только нужные три поля.
            var items = await _db.PurchaseItems
                .AsNoTracking()
                .Select(x => new
                {
                    x.ProductId,
                    x.Quantity,
                    Date = x.Purchase.PurchaseDate
                })
                .ToListAsync(ct);

            return items
                .Where(x => from is null || x.Date >= from.Value)
                .Where(x => toExclusive is null || x.Date < toExclusive.Value)
                .GroupBy(x => x.ProductId)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));
        }
    }
}
