using Application.Contracts.Interfaces;
using Application.Contracts.Persistence;
using Application.DTOs.Dashboard;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Services
{
    /// <summary>
    /// Сводка для главной страницы. Даты в SQLite лежат текстом и не сравниваются в SQL,
    /// поэтому из базы забираются короткие проекции, а периоды считаются в памяти.
    /// </summary>
    public class DashboardService : IDashboardService
    {
        private readonly IDataContext _db;

        public DashboardService(IDataContext db)
        {
            _db = db;
        }

        public async Task<DashboardResponse> GetSnapshotAsync(
            int revenueDays,
            int topDays,
            int lowStockThreshold,
            CancellationToken ct)
        {
            var today = DateTime.Today;

            var sales = await _db.Sales
                .AsNoTracking()
                .Select(s => new
                {
                    s.Id,
                    s.SaleDate,
                    s.TotalAmount,
                    s.PaidAmount,
                    s.PaymentMethod,
                })
                .ToListAsync(ct);

            // Дата продажи хранится в UTC — на витрине нужен местный день
            var byDay = sales
                .Select(s => new { s.Id, Day = s.SaleDate.ToLocalTime().Date, s.TotalAmount, s.PaidAmount, s.PaymentMethod })
                .ToList();

            var todaySales = byDay.Where(s => s.Day == today).ToList();

            var response = new DashboardResponse
            {
                TodayRevenue   = todaySales.Sum(s => s.TotalAmount),
                TodayReceipts  = todaySales.Count,
                AverageReceipt = todaySales.Count == 0 ? 0m : todaySales.Sum(s => s.TotalAmount) / todaySales.Count,
                TotalDebt      = byDay.Where(s => s.PaidAmount < s.TotalAmount).Sum(s => s.TotalAmount - s.PaidAmount),
                DebtorsCount   = byDay.Count(s => s.PaidAmount < s.TotalAmount),
            };

            response.Payments = todaySales
                .GroupBy(s => s.PaymentMethod)
                .Select(g => new PaymentSliceResponse
                {
                    Method   = g.Key,
                    Amount   = g.Sum(s => s.TotalAmount),
                    Receipts = g.Count(),
                })
                .OrderByDescending(p => p.Amount)
                .ToList();

            // График: пустые дни тоже нужны, иначе столбики врут о динамике
            var revenueByDay = byDay
                .GroupBy(s => s.Day)
                .ToDictionary(g => g.Key, g => (Amount: g.Sum(s => s.TotalAmount), Count: g.Count()));

            response.Revenue = Enumerable.Range(0, revenueDays)
                .Select(offset => today.AddDays(offset - revenueDays + 1))
                .Select(day => new DailyRevenueResponse
                {
                    Date     = day,
                    Amount   = revenueByDay.TryGetValue(day, out var d) ? d.Amount : 0m,
                    Receipts = revenueByDay.TryGetValue(day, out var c) ? c.Count : 0,
                })
                .ToList();

            await FillSalesDetailsAsync(response, byDay.ToDictionary(s => s.Id, s => s.Day), today, topDays, ct);
            await FillStockAsync(response, lowStockThreshold, ct);
            await FillMovementsAsync(response, today, ct);

            return response;
        }

        // Прибыль за сегодня и топ товаров за период
        private async Task FillSalesDetailsAsync(
            DashboardResponse response,
            Dictionary<long, DateTime> saleDays,
            DateTime today,
            int topDays,
            CancellationToken ct)
        {
            var items = await _db.SaleItems
                .AsNoTracking()
                .Select(i => new
                {
                    i.SaleId,
                    i.ProductId,
                    i.Quantity,
                    i.PriceAtSale,
                    ProductName = i.Product.Name,
                    Cost        = i.Product.CostPerUnit,
                })
                .ToListAsync(ct);

            var withDay = items
                .Where(i => saleDays.ContainsKey(i.SaleId))
                .Select(i => new { Day = saleDays[i.SaleId], i.ProductId, i.Quantity, i.PriceAtSale, i.ProductName, i.Cost })
                .ToList();

            // Себестоимость берётся из карточки товара: цена закупки на момент продажи
            // нигде не сохраняется, поэтому прибыль здесь оценочная
            var todayItems = withDay.Where(i => i.Day == today).ToList();

            decimal soldCost = todayItems.Sum(i => i.Quantity * (i.Cost ?? 0m));

            response.TodayProfit = response.TodayRevenue - soldCost;

            var from = today.AddDays(-topDays + 1);

            response.TopProducts = withDay
                .Where(i => i.Day >= from)
                .GroupBy(i => new { i.ProductId, i.ProductName })
                .Select(g => new TopProductResponse
                {
                    ProductId = g.Key.ProductId,
                    Name      = g.Key.ProductName,
                    Quantity  = g.Sum(i => i.Quantity),
                    Revenue   = g.Sum(i => i.Quantity * i.PriceAtSale),
                })
                .OrderByDescending(p => p.Revenue)
                .Take(5)
                .ToList();
        }

        // Товар, который заканчивается, и проблемные карточки
        private async Task FillStockAsync(DashboardResponse response, int threshold, CancellationToken ct)
        {
            var products = await _db.Products
                .AsNoTracking()
                .Select(p => new
                {
                    p.Id,
                    p.Name,
                    p.Barcode,
                    p.PricePerUnit,
                    CategoryName = p.Category.Name,
                    InStock      = p.Inventory != null ? p.Inventory.Quantity : 0,
                })
                .ToListAsync(ct);

            response.ProductsWithoutPrice = products.Count(p => p.PricePerUnit <= 0);
            response.ProductsOutOfStock   = products.Count(p => p.InStock <= 0);

            response.LowStock = products
                .Where(p => p.InStock <= threshold)
                .OrderBy(p => p.InStock)
                .ThenBy(p => p.Name)
                .Take(8)
                .Select(p => new LowStockResponse
                {
                    ProductId    = p.Id,
                    Name         = p.Name,
                    Barcode      = p.Barcode,
                    CategoryName = p.CategoryName,
                    InStock      = p.InStock,
                })
                .ToList();
        }

        // Закупки и списания за сегодня
        private async Task FillMovementsAsync(DashboardResponse response, DateTime today, CancellationToken ct)
        {
            var purchaseItems = await _db.PurchaseItems
                .AsNoTracking()
                .Select(i => new { i.Quantity, i.CostPerUnit, Date = i.Purchase.PurchaseDate })
                .ToListAsync(ct);

            response.TodayPurchases = purchaseItems
                .Where(i => i.Date.ToLocalTime().Date == today)
                .Sum(i => i.Quantity * i.CostPerUnit);

            var writeOffs = await _db.StockWriteOffs
                .AsNoTracking()
                .Select(w => new { w.Quantity, w.CreatedAt })
                .ToListAsync(ct);

            response.TodayWrittenOff = writeOffs
                .Where(w => w.CreatedAt.ToLocalTime().Date == today)
                .Sum(w => w.Quantity);
        }
    }
}
