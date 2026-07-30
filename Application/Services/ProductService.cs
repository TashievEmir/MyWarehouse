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
                    PricePerUnit = p.PricePerUnit,
                    CostPerUnit = p.CostPerUnit
                })
                .FirstOrDefaultAsync(ct);
        }

        public async Task<List<ProductListItemResponse>> GetCatalogAsync(string? search, CancellationToken ct)
        {
            var catalog = await _db.Products
                .AsNoTracking()
                .OrderBy(p => p.Name)
                .Select(p => new ProductListItemResponse
                {
                    ProductId = p.Id,
                    Name = p.Name,
                    SKU = p.SKU,
                    Barcode = p.Barcode,
                    Description = p.Description,
                    CategoryId = p.CategoryId,
                    CategoryName = p.Category.Name,
                    PricePerUnit = p.PricePerUnit,
                    CostPerUnit = p.CostPerUnit,
                    InStock = p.Inventory != null ? p.Inventory.Quantity : 0,
                    HasHistory = p.SaleItems.Any()
                                 || p.PurchaseItems.Any()
                                 || _db.StockWriteOffs.Any(w => w.ProductId == p.Id)
                })
                .ToListAsync(ct);

            if (string.IsNullOrWhiteSpace(search))
                return catalog;

            // LIKE в SQLite не учитывает регистр только для латиницы, поэтому
            // «кола» не нашла бы «Кола» — ищем в памяти по правилам культуры.
            var term = search.Trim();

            return catalog
                .Where(p =>
                    Contains(p.Name, term) ||
                    Contains(p.SKU, term) ||
                    Contains(p.Barcode, term) ||
                    Contains(p.CategoryName, term))
                .ToList();
        }

        private static bool Contains(string? value, string term)
            => value is not null && value.Contains(term, StringComparison.CurrentCultureIgnoreCase);

        public async Task UpdateAsync(UpdateProductRequest request, CancellationToken ct)
        {
            var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == request.ProductId, ct)
                ?? throw new DomainException("Товар не найден");

            var name = (request.Name ?? "").Trim();

            if (name.Length == 0)
                throw new DomainException("Укажите название товара");

            if (request.PricePerUnit <= 0)
                throw new DomainException("Укажите цену продажи");

            if (request.CostPerUnit is < 0)
                throw new DomainException("Цена закупки не может быть отрицательной");

            if (!await _db.Categories.AnyAsync(c => c.Id == request.CategoryId, ct))
                throw new DomainException("Категория не найдена");

            var barcode = string.IsNullOrWhiteSpace(request.Barcode) ? null : request.Barcode.Trim();

            // Штрихкод уникален: без своей проверки пользователь увидел бы ошибку SQLite
            if (barcode is not null &&
                await _db.Products.AnyAsync(p => p.Barcode == barcode && p.Id != product.Id, ct))
            {
                throw new DomainException($"Штрихкод {barcode} уже занят другим товаром");
            }

            var sku = (request.SKU ?? "").Trim();

            product.Name = name;
            product.SKU = sku.Length > 0 ? sku : barcode ?? product.SKU;
            product.Barcode = barcode;
            product.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
            product.CategoryId = request.CategoryId;
            product.PricePerUnit = request.PricePerUnit;
            product.CostPerUnit = request.CostPerUnit;

            await _db.SaveChangesAsync(ct);
        }

        public async Task WriteOffAsync(WriteOffProductRequest request, CancellationToken ct)
        {
            if (request.Quantity <= 0)
                throw new DomainException("Количество должно быть больше нуля");

            if (request.UserId <= 0)
                throw new DomainException("Не указан сотрудник");

            var product = await _db.Products
                .Where(p => p.Id == request.ProductId)
                .Select(p => new { p.Id, p.Name })
                .FirstOrDefaultAsync(ct)
                ?? throw new DomainException("Товар не найден");

            await using var tx = await _db.BeginTransactionAsync(ct);

            try
            {
                var inventory = await _db.Inventories
                    .FirstOrDefaultAsync(x => x.ProductId == request.ProductId, ct)
                    ?? throw new DomainException("Остатка по товару нет");

                if (inventory.Quantity < request.Quantity)
                    throw new DomainException($"«{product.Name}»: на складе {inventory.Quantity} шт., списать {request.Quantity} нельзя");

                inventory.Decrease(request.Quantity);

                _db.StockWriteOffs.Add(new StockWriteOff(
                    request.ProductId,
                    request.UserId,
                    request.Quantity,
                    request.Reason,
                    string.IsNullOrWhiteSpace(request.Comment) ? null : request.Comment.Trim()));

                await _db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
            }
            catch
            {
                await tx.RollbackAsync(ct);
                throw;
            }
        }

        public async Task DeleteAsync(long productId, CancellationToken ct)
        {
            var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == productId, ct)
                ?? throw new DomainException("Товар не найден");

            if (await _db.SaleItems.AnyAsync(x => x.ProductId == productId, ct))
                throw new DomainException($"«{product.Name}» продавался — удаление стёрло бы историю продаж. Спишите остаток вместо удаления");

            if (await _db.PurchaseItems.AnyAsync(x => x.ProductId == productId, ct))
                throw new DomainException($"«{product.Name}» есть в поставках — удаление стёрло бы историю приходов. Спишите остаток вместо удаления");

            if (await _db.StockWriteOffs.AnyAsync(x => x.ProductId == productId, ct))
                throw new DomainException($"По «{product.Name}» уже были списания — карточку удалять нельзя");

            var inventory = await _db.Inventories.FirstOrDefaultAsync(x => x.ProductId == productId, ct);

            if (inventory is not null)
                _db.Inventories.Remove(inventory);

            _db.Products.Remove(product);

            await _db.SaveChangesAsync(ct);
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

                    if (item.Cost < 0 || item.Price < 0)
                        throw new DomainException("Цена не может быть отрицательной");

                    var product = await ResolveProductAsync(item, ct);

                    // Приход обновляет карточку: цена продажи уходит на кассу,
                    // закупочная нужна для расчёта прибыли. Ноль — «не менять».
                    if (item.Price > 0)
                        product.PricePerUnit = item.Price;

                    if (item.Cost > 0)
                        product.CostPerUnit = item.Cost;

                    if (lines.TryGetValue(product.Id, out var line))
                        lines[product.Id] = (line.Quantity + item.Quantity, line.Cost > 0 ? line.Cost : item.Cost);
                    else
                        lines[product.Id] = (item.Quantity, item.Cost);
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
        private async Task<Product> ResolveProductAsync(ReceiveItemRequest item, CancellationToken ct)
        {
            var barcode = string.IsNullOrWhiteSpace(item.Barcode) ? null : item.Barcode.Trim();

            if (item.ProductId is > 0)
            {
                return await _db.Products.FirstOrDefaultAsync(p => p.Id == item.ProductId, ct)
                    ?? throw new DomainException("Товар не найден");
            }

            // Товар мог появиться уже после сканирования — не заводим дубль
            if (barcode is not null)
            {
                var existing = await _db.Products.FirstOrDefaultAsync(p => p.Barcode == barcode, ct);

                if (existing is not null)
                    return existing;
            }

            var name = (item.Name ?? "").Trim();

            if (name.Length == 0)
                throw new DomainException($"У нового товара {barcode} не указано название");

            if (item.CategoryId is not > 0)
                throw new DomainException($"У нового товара «{name}» не выбрана категория");

            // Без цены продажи товар нельзя пробить на кассе
            if (item.Price <= 0)
                throw new DomainException($"У нового товара «{name}» не указана цена продажи");

            var categoryExists = await _db.Categories.AnyAsync(c => c.Id == item.CategoryId, ct);

            if (!categoryExists)
                throw new DomainException("Категория не найдена");

            var product = new Product
            {
                Name = name,
                SKU = barcode ?? $"SKU-{Guid.NewGuid():N}"[..12],
                Barcode = barcode,
                CategoryId = item.CategoryId.Value,
                PricePerUnit = item.Price,
                CostPerUnit = item.Cost,
                CreatedAt = DateTimeOffset.UtcNow
            };

            _db.Products.Add(product);
            await _db.SaveChangesAsync(ct);

            return product;
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
