using Application.Contracts.Interfaces;
using Application.Contracts.Persistence;
using Application.DTOs.Labels;
using Application.DTOs.Printing;
using Application.Localization;
using Domain.Enums;
using Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Application.Services
{
    public class LabelService : ILabelService
    {
        private readonly IDataContext _db;
        private readonly IPrinterSettingsService _printerSettings;
        private readonly IReceiptPrinter _printer;
        private readonly IActivityLogService _activity;

        public LabelService(
            IDataContext db,
            IPrinterSettingsService printerSettings,
            IReceiptPrinter printer,
            IActivityLogService activity)
        {
            _db = db;
            _printerSettings = printerSettings;
            _printer = printer;
            _activity = activity;
        }

        public async Task<List<LabelProductResponse>> GetProductsAsync(
            string? search,
            bool onlyWithoutBarcode,
            CancellationToken ct)
        {
            var query = _db.Products.AsNoTracking();

            if (onlyWithoutBarcode)
                query = query.Where(p => p.Barcode == null || p.Barcode == "");

            var products = await query
                .OrderBy(p => p.Name)
                .Select(p => new LabelProductResponse
                {
                    ProductId = p.Id,
                    Name = p.Name,
                    SKU = p.SKU,
                    Barcode = p.Barcode,
                    CategoryName = p.Category.Name,
                    PricePerUnit = p.PricePerUnit,
                    InStock = p.Inventory != null ? p.Inventory.Quantity : 0,
                })
                .ToListAsync(ct);

            foreach (var product in products)
                product.IsInternalBarcode = BarcodeGenerator.IsInternal(product.Barcode);

            if (string.IsNullOrWhiteSpace(search))
                return products;

            // Регистронезависимый поиск по кириллице SQLite не умеет — фильтруем в памяти
            var term = search.Trim();

            return products
                .Where(p => Contains(p.Name, term)
                            || Contains(p.SKU, term)
                            || Contains(p.Barcode, term)
                            || Contains(p.CategoryName, term))
                .ToList();
        }

        private static bool Contains(string? value, string term)
            => value is not null && value.Contains(term, StringComparison.CurrentCultureIgnoreCase);

        public async Task<string> GenerateBarcodeAsync(long productId, long userId, CancellationToken ct)
        {
            var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == productId, ct)
                ?? throw new DomainException(Tr.T("Err_ProductNotFound"));

            // Номер берём от наибольшего выданного, а не от количества товаров:
            // код удалённого товара переиспользовать нельзя, его этикетки
            // могли остаться наклеенными на остатках
            var issued = await _db.Products
                .AsNoTracking()
                .Where(p => p.Barcode != null)
                .Select(p => p.Barcode)
                .ToListAsync(ct);

            var barcode = BarcodeGenerator.Compose(BarcodeGenerator.NextSequence(issued));

            // Коллизия почти невозможна, но проверка дешевле разбора последствий
            if (await _db.Products.AnyAsync(p => p.Barcode == barcode, ct))
                throw new DomainException(Tr.F("Err_BarcodeTaken", barcode));

            product.Barcode = barcode;

            await _db.SaveChangesAsync(ct);

            await LogAsync(userId, product.Name, barcode, generated: true, productId, ct);

            return barcode;
        }

        public async Task SetBarcodeAsync(long productId, string barcode, long userId, CancellationToken ct)
        {
            barcode = (barcode ?? "").Trim();

            if (!BarcodeGenerator.IsValid(barcode))
                throw new DomainException(Tr.T("Err_BadBarcode"));

            var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == productId, ct)
                ?? throw new DomainException(Tr.T("Err_ProductNotFound"));

            if (await _db.Products.AnyAsync(p => p.Barcode == barcode && p.Id != productId, ct))
                throw new DomainException(Tr.F("Err_BarcodeTaken", barcode));

            product.Barcode = barcode;

            await _db.SaveChangesAsync(ct);

            await LogAsync(userId, product.Name, barcode, generated: false, productId, ct);
        }

        public async Task PrintLabelsAsync(long productId, LabelOptions options, CancellationToken ct)
        {
            var product = await _db.Products
                .AsNoTracking()
                .Where(p => p.Id == productId)
                .Select(p => new LabelProductResponse
                {
                    ProductId = p.Id,
                    Name = p.Name,
                    SKU = p.SKU,
                    Barcode = p.Barcode,
                    CategoryName = p.Category.Name,
                    PricePerUnit = p.PricePerUnit,
                })
                .FirstOrDefaultAsync(ct)
                ?? throw new DomainException(Tr.T("Err_ProductNotFound"));

            if (!product.HasBarcode)
                throw new DomainException(Tr.T("Err_NoBarcodeToPrint"));

            var settings = await _printerSettings.GetAsync(ct);

            var lines = LabelRenderer.RenderSheet(product, options, settings.CharsPerLine);

            await _printer.PrintAsync(new ReceiptDocument
            {
                PrinterName = settings.PrinterName,
                Codepage = settings.Codepage,
                Lines = lines,
                CutPaper = settings.CutPaper,
                // Этикетки к деньгам отношения не имеют — ящик не трогаем
                OpenCashDrawer = false,
                FeedLines = settings.FeedLines,
                JobName = $"Labels: {product.Name}",
            }, ct);
        }

        private Task LogAsync(long userId, string productName, string barcode, bool generated, long productId, CancellationToken ct)
            => _activity.LogAsync(
                userId,
                ActivityType.BarcodeAssigned,
                Tr.T(generated ? "Log_BarcodeGenerated" : "Log_BarcodeSet"),
                Tr.F("Log_BarcodeDetails", productName, barcode),
                "Product",
                productId,
                ct);
    }
}
