using Application.Contracts.Interfaces;
using Application.Contracts.Persistence;
using Application.DTOs.Products;
using Domain.Entities;
using Domain.Enums;
using Domain.Exceptions;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

using Application.Localization;

namespace Application.Services
{
    public class ProductService : IProductService
    {
        private readonly IDataContext _db;
        private readonly IActivityLogService _activity;

        public ProductService(IDataContext db, IActivityLogService activity)
        {
            _db = db;
            _activity = activity;
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
                ?? throw new DomainException(Tr.T("Err_ProductNotFound"));

            var name = (request.Name ?? "").Trim();

            if (name.Length == 0)
                throw new DomainException(Tr.T("Err_NeedProductName"));

            if (request.PricePerUnit <= 0)
                throw new DomainException(Tr.T("Err_NeedSalePrice"));

            if (request.CostPerUnit is < 0)
                throw new DomainException(Tr.T("Err_CostNegative"));

            if (!await _db.Categories.AnyAsync(c => c.Id == request.CategoryId, ct))
                throw new DomainException(Tr.T("Err_CategoryNotFound"));

            var barcode = string.IsNullOrWhiteSpace(request.Barcode) ? null : request.Barcode.Trim();

            // Штрихкод уникален: без своей проверки пользователь увидел бы ошибку SQLite
            if (barcode is not null &&
                await _db.Products.AnyAsync(p => p.Barcode == barcode && p.Id != product.Id, ct))
            {
                throw new DomainException(Tr.F("Err_BarcodeTaken", barcode));
            }

            var oldPrice = product.PricePerUnit;
            var oldName  = product.Name;

            var sku = (request.SKU ?? "").Trim();

            product.Name = name;
            product.SKU = sku.Length > 0 ? sku : barcode ?? product.SKU;
            product.Barcode = barcode;
            product.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
            product.CategoryId = request.CategoryId;
            product.PricePerUnit = request.PricePerUnit;
            product.CostPerUnit = request.CostPerUnit;

            await _db.SaveChangesAsync(ct);

            if (oldPrice != product.PricePerUnit)
            {
                await _activity.LogAsync(
                    request.UserId,
                    ActivityType.PriceChanged,
                    Tr.T("Log_PriceChanged"),
                    $"{product.Name}: {oldPrice:N2} → {product.PricePerUnit:N2}",
                    "Product",
                    product.Id,
                    ct);
            }
            else if (oldName != product.Name)
            {
                await _activity.LogAsync(
                    request.UserId,
                    ActivityType.PriceChanged,
                    Tr.T("Log_ProductChanged"),
                    $"{oldName} → {product.Name}",
                    "Product",
                    product.Id,
                    ct);
            }
        }

        public async Task WriteOffAsync(WriteOffProductRequest request, CancellationToken ct)
        {
            if (request.Quantity <= 0)
                throw new DomainException(Tr.T("Err_QuantityPositive"));

            if (request.UserId <= 0)
                throw new DomainException(Tr.T("Err_NoEmployee"));

            var product = await _db.Products
                .Where(p => p.Id == request.ProductId)
                .Select(p => new { p.Id, p.Name })
                .FirstOrDefaultAsync(ct)
                ?? throw new DomainException(Tr.T("Err_ProductNotFound"));

            await using var tx = await _db.BeginTransactionAsync(ct);

            try
            {
                var inventory = await _db.Inventories
                    .FirstOrDefaultAsync(x => x.ProductId == request.ProductId, ct)
                    ?? throw new DomainException(Tr.T("Err_NoInventory"));

                if (inventory.Quantity < request.Quantity)
                    throw new DomainException(Tr.F("Err_WriteOffTooMuch", product.Name, inventory.Quantity, request.Quantity));

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

            var reason = request.Reason switch
            {
                WriteOffReason.Damage           => Tr.T("Reason_Damage"),
                WriteOffReason.Shortage         => Tr.T("Reason_Shortage"),
                WriteOffReason.ReturnToSupplier => Tr.T("Reason_ReturnToSupplier"),
                _                               => Tr.T("Reason_Other"),
            };

            var details = Tr.F("Log_WriteOffDetails", product.Name, request.Quantity, reason);

            if (!string.IsNullOrWhiteSpace(request.Comment))
                details += $" · {request.Comment.Trim()}";

            await _activity.LogAsync(
                request.UserId,
                ActivityType.StockWrittenOff,
                Tr.T("Log_WriteOff"),
                details,
                "Product",
                request.ProductId,
                ct);
        }

        public async Task AdjustStockAsync(AdjustStockRequest request, CancellationToken ct)
        {
            if (request.Quantity < 0)
                throw new DomainException(Tr.T("Err_QuantityNegative"));

            if (request.UserId <= 0)
                throw new DomainException(Tr.T("Err_NoEmployee"));

            var product = await _db.Products
                .Where(p => p.Id == request.ProductId)
                .Select(p => new { p.Id, p.Name })
                .FirstOrDefaultAsync(ct)
                ?? throw new DomainException(Tr.T("Err_ProductNotFound"));

            var inventory = await _db.Inventories
                .FirstOrDefaultAsync(x => x.ProductId == request.ProductId, ct);

            if (inventory is null)
            {
                inventory = new Inventory(request.ProductId, request.Quantity);
                _db.Inventories.Add(inventory);
            }

            var was = inventory.Quantity;

            if (was == request.Quantity)
                return;

            inventory.SetQuantity(request.Quantity);

            await _db.SaveChangesAsync(ct);

            await _activity.LogAsync(
                request.UserId,
                ActivityType.StockAdjusted,
                Tr.T("Log_StockAdjusted"),
                Tr.F("Log_StockAdjustedDetails", product.Name, was, request.Quantity),
                "Product",
                request.ProductId,
                ct);
        }

        public async Task DeleteAsync(long productId, CancellationToken ct)
        {
            var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == productId, ct)
                ?? throw new DomainException(Tr.T("Err_ProductNotFound"));

            if (await _db.SaleItems.AnyAsync(x => x.ProductId == productId, ct))
                throw new DomainException(Tr.F("Err_DeleteSold", product.Name));

            if (await _db.PurchaseItems.AnyAsync(x => x.ProductId == productId, ct))
                throw new DomainException(Tr.F("Err_DeletePurchased", product.Name));

            if (await _db.StockWriteOffs.AnyAsync(x => x.ProductId == productId, ct))
                throw new DomainException(Tr.F("Err_DeleteWrittenOff", product.Name));

            var inventory = await _db.Inventories.FirstOrDefaultAsync(x => x.ProductId == productId, ct);

            if (inventory is not null)
                _db.Inventories.Remove(inventory);

            _db.Products.Remove(product);

            await _db.SaveChangesAsync(ct);
        }

        public async Task<long> ReceiveAsync(ReceiveProductsRequest request, CancellationToken ct)
        {
            if (request.Items.Count == 0)
                throw new DomainException(Tr.T("Err_ProductListEmpty"));

            await using var tx = await _db.BeginTransactionAsync(ct);

            try
            {
                // Один товар может встретиться в списке несколько раз — в покупке
                // позиция на товар только одна, поэтому количества складываем.
                var lines = new Dictionary<long, (int Quantity, decimal Cost)>();

                // Названия заведённых на лету товаров — отдельными событиями в историю
                var created = new List<string>();

                foreach (var item in request.Items)
                {
                    if (item.Quantity <= 0)
                        throw new DomainException(Tr.T("Err_QuantityPositive"));

                    if (item.Cost < 0 || item.Price < 0)
                        throw new DomainException(Tr.T("Err_PriceNegative"));

                    var product = await ResolveProductAsync(item, created, ct);

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

                var (supplierId, supplier) = await ResolveSupplierAsync(request, ct);

                var purchase = new Purchase(supplier, supplierId);

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

                foreach (var name in created)
                {
                    await _activity.LogAsync(
                        request.UserId,
                        ActivityType.ProductCreated,
                        Tr.T("Log_ProductCreated"),
                        name,
                        "Purchase",
                        purchase.Id,
                        ct);
                }

                await _activity.LogAsync(
                    request.UserId,
                    ActivityType.PurchaseSaved,
                    Tr.T("Log_PurchaseSaved"),
                    Tr.F("Log_PurchaseDetails", supplier, lines.Count, lines.Sum(l => l.Value.Quantity), purchase.TotalCost.ToString("N2")),
                    "Purchase",
                    purchase.Id,
                    ct);

                return purchase.Id;
            }
            catch
            {
                await tx.RollbackAsync(ct);
                throw;
            }
        }

        /// <summary>
        /// Поставщик берётся из справочника по Id, а введённое вручную имя туда
        /// добавляется — в следующий раз оно уже будет в выпадающем списке.
        /// Приход без поставщика допустим: тогда пишем «Без поставщика» без ссылки.
        /// </summary>
        private async Task<(long? Id, string Name)> ResolveSupplierAsync(ReceiveProductsRequest request, CancellationToken ct)
        {
            if (request.SupplierId is > 0)
            {
                var picked = await _db.Suppliers
                    .FirstOrDefaultAsync(s => s.Id == request.SupplierId, ct)
                    ?? throw new DomainException(Tr.T("Err_SupplierNotFound"));

                return (picked.Id, picked.Name);
            }

            var name = (request.SupplierName ?? "").Trim();

            if (name.Length == 0)
                return (null, Tr.T("Log_NoSupplier"));

            // Регистр сверяем в памяти: LIKE в SQLite нечувствителен только к латинице
            var all = await _db.Suppliers.ToListAsync(ct);

            var existing = all.FirstOrDefault(s =>
                string.Equals(s.Name, name, StringComparison.CurrentCultureIgnoreCase));

            if (existing is not null)
                return (existing.Id, existing.Name);

            var supplier = new Supplier(name);

            _db.Suppliers.Add(supplier);
            await _db.SaveChangesAsync(ct);

            return (supplier.Id, supplier.Name);
        }

        /// <summary>
        /// Находит товар позиции или заводит новый — тогда он сразу сохраняется,
        /// чтобы получить Id для остатка и позиции покупки.
        /// </summary>
        private async Task<Product> ResolveProductAsync(ReceiveItemRequest item, List<string> created, CancellationToken ct)
        {
            var barcode = string.IsNullOrWhiteSpace(item.Barcode) ? null : item.Barcode.Trim();

            if (item.ProductId is > 0)
            {
                return await _db.Products.FirstOrDefaultAsync(p => p.Id == item.ProductId, ct)
                    ?? throw new DomainException(Tr.T("Err_ProductNotFound"));
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
                throw new DomainException(Tr.F("Err_NewProductNoName", barcode));

            if (item.CategoryId is not > 0)
                throw new DomainException(Tr.F("Err_NewProductNoCategory", name));

            // Без цены продажи товар нельзя пробить на кассе
            if (item.Price <= 0)
                throw new DomainException(Tr.F("Err_NewProductNoPrice", name));

            var categoryExists = await _db.Categories.AnyAsync(c => c.Id == item.CategoryId, ct);

            if (!categoryExists)
                throw new DomainException(Tr.T("Err_CategoryNotFound"));

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

            created.Add(product.Name);

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
