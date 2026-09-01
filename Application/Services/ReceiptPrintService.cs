using Application.Contracts.Interfaces;
using Application.DTOs.Printing;
using Application.Localization;
using Domain.Enums;
using Domain.Exceptions;

namespace Application.Services
{
    public class ReceiptPrintService : IReceiptPrintService
    {
        private readonly IPrinterSettingsService _settings;
        private readonly IReceiptTemplateService _templates;
        private readonly ISalesService _sales;
        private readonly IReceiptPrinter _printer;

        public ReceiptPrintService(
            IPrinterSettingsService settings,
            IReceiptTemplateService templates,
            ISalesService sales,
            IReceiptPrinter printer)
        {
            _settings = settings;
            _templates = templates;
            _sales = sales;
            _printer = printer;
        }

        public async Task<bool> IsAutoPrintEnabledAsync(CancellationToken ct)
            => (await _settings.GetAsync(ct)).AutoPrint;

        public async Task PrintSaleAsync(long saleId, decimal? cashGiven, decimal? change, CancellationToken ct)
        {
            var receipt = await _sales.GetReceiptAsync(saleId, ct)
                ?? throw new DomainException(Tr.F("Print_SaleNotFound", saleId));

            var settings = await _settings.GetAsync(ct);
            var template = await _templates.GetAsync(ct);

            var lines = ReceiptRenderer.Render(
                template,
                receipt,
                settings.CharsPerLine,
                new ReceiptRenderer.CashInfo { CashGiven = cashGiven, Change = change });

            // Ящик дёргаем только под наличные: по карте и в долг открывать нечего
            var openDrawer = settings.OpenCashDrawer && receipt.PaymentMethod == PaymentMethod.Cash;

            await PrintAsync(lines, settings, openDrawer, $"Receipt #{saleId}", ct);
        }

        public async Task PrintTestAsync(CancellationToken ct)
        {
            var settings = await _settings.GetAsync(ct);
            var template = await _templates.GetAsync(ct);

            var lines = ReceiptRenderer.RenderTest(template, settings.CharsPerLine);

            await PrintAsync(lines, settings, settings.OpenCashDrawer, "Receipt test", ct);
        }

        private Task PrintAsync(
            List<ReceiptPrintLine> lines,
            PrinterSettingsResponse settings,
            bool openDrawer,
            string jobName,
            CancellationToken ct)
        {
            var document = new ReceiptDocument
            {
                PrinterName = settings.PrinterName,
                Codepage = settings.Codepage,
                Lines = lines,
                CutPaper = settings.CutPaper,
                OpenCashDrawer = openDrawer,
                FeedLines = settings.FeedLines,
                JobName = jobName,
            };

            return _printer.PrintAsync(document, ct);
        }
    }
}
