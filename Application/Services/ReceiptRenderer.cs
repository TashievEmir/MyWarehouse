using System.Globalization;
using Application.DTOs.Printing;
using Application.DTOs.Receipts;
using Application.DTOs.Sales;
using Application.Localization;
using Domain.Enums;

namespace Application.Services
{
    /// <summary>
    /// Собирает ленту чека из шаблона и данных продажи. Здесь же живёт вся
    /// вёрстка под узкую ленту: перенос длинных названий и выравнивание сумм
    /// по правому краю. Печать и предпросмотр ходят сюда вместе, чтобы на
    /// экране и на бумаге получался один и тот же чек.
    /// </summary>
    public static class ReceiptRenderer
    {
        /// <summary>Данные, которых нет в сохранённом чеке: их знает только касса.</summary>
        public class CashInfo
        {
            public decimal? CashGiven { get; set; }
            public decimal? Change { get; set; }
        }

        public static List<ReceiptPrintLine> Render(
            ReceiptTemplateResponse template,
            ReceiptDetailsResponse receipt,
            int width,
            CashInfo? cash = null)
        {
            var enabled = template.Blocks
                .Where(b => b.IsEnabled)
                .Select(b => b.Key)
                .ToHashSet();

            var lines = new List<ReceiptPrintLine>();

            // ---------- Шапка ----------

            if (enabled.Contains("logo") && !string.IsNullOrWhiteSpace(template.ShopName))
            {
                lines.Add(ReceiptPrintLine.Of(
                    template.ShopName.ToUpper(CultureInfo.CurrentCulture),
                    PrintAlign.Center,
                    bold: true,
                    doubleHeight: true));
            }

            if (enabled.Contains("address"))
            {
                if (!string.IsNullOrWhiteSpace(template.Address))
                    AddWrapped(lines, template.Address!, width, PrintAlign.Center);

                if (!string.IsNullOrWhiteSpace(template.Tin))
                    lines.Add(ReceiptPrintLine.Of(Tr.F("Receipt_Tin", template.Tin), PrintAlign.Center));
            }

            AddSeparator(lines, width);

            // ---------- Номер и кассир ----------

            if (enabled.Contains("number"))
            {
                lines.Add(ReceiptPrintLine.Of(Tr.F("Receipt_Number", receipt.SaleId)));
                lines.Add(ReceiptPrintLine.Of(
                    receipt.SaleDate.ToLocalTime().ToString("dd.MM.yyyy HH:mm", CultureInfo.CurrentCulture)));
            }

            if (enabled.Contains("cashier") && !string.IsNullOrWhiteSpace(receipt.CashierName))
                lines.Add(ReceiptPrintLine.Of(Tr.F("Receipt_Cashier", receipt.CashierName)));

            AddSeparator(lines, width);

            // ---------- Позиции ----------

            var showBarcode = enabled.Contains("barcode");

            foreach (var item in receipt.Lines)
            {
                AddWrapped(lines, item.ProductName, width);

                if (showBarcode && !string.IsNullOrWhiteSpace(item.Barcode))
                    lines.Add(ReceiptPrintLine.Of("  " + item.Barcode));

                // «2 x 45.00» слева, сумма позиции справа
                var left = $"  {item.Quantity} x {Money(item.Price)}";

                lines.Add(ReceiptPrintLine.Of(Columns(left, Money(item.Total), width)));
            }

            AddSeparator(lines, width);

            // ---------- Итоги ----------

            if (receipt.DiscountAmount > 0)
            {
                lines.Add(ReceiptPrintLine.Of(Columns(Tr.T("Receipt_Subtotal"), Money(receipt.Subtotal), width)));
                lines.Add(ReceiptPrintLine.Of(Columns(Tr.T("Receipt_Discount"), Money(receipt.DiscountAmount), width)));
            }

            lines.Add(ReceiptPrintLine.Of(
                Columns(Tr.T("Receipt_Total"), Money(receipt.TotalAmount), width),
                bold: true));

            if (cash?.CashGiven is > 0)
                lines.Add(ReceiptPrintLine.Of(Columns(Tr.T("Receipt_Cash"), Money(cash.CashGiven!.Value), width)));

            if (cash?.Change is > 0)
                lines.Add(ReceiptPrintLine.Of(Columns(Tr.T("Receipt_Change"), Money(cash.Change!.Value), width)));

            lines.Add(ReceiptPrintLine.Of(Tr.F("Receipt_Payment", PaymentName(receipt.PaymentMethod))));

            // ---------- Клиент и долг ----------

            if (enabled.Contains("customer") && !string.IsNullOrWhiteSpace(receipt.CustomerName))
            {
                AddSeparator(lines, width);

                lines.Add(ReceiptPrintLine.Of(Tr.F("Receipt_Customer", receipt.CustomerName)));

                if (receipt.DebtLeft > 0)
                {
                    lines.Add(ReceiptPrintLine.Of(
                        Columns(Tr.T("Receipt_Debt"), Money(receipt.DebtLeft), width),
                        bold: true));
                }
            }

            // Возврат печатаем крупно: такой чек не должен путаться с обычным
            if (receipt.IsReturned)
            {
                AddSeparator(lines, width);
                lines.Add(ReceiptPrintLine.Of(Tr.T("Receipt_Returned"), PrintAlign.Center, bold: true, doubleHeight: true));
            }

            // ---------- Подвал ----------

            if (!string.IsNullOrWhiteSpace(template.FooterText))
            {
                AddSeparator(lines, width);
                AddWrapped(lines, template.FooterText!, width, PrintAlign.Center);
            }

            return lines;
        }

        /// <summary>Тестовый чек: проверяет кириллицу, колонки, жирный шрифт и обрезку.</summary>
        public static List<ReceiptPrintLine> RenderTest(ReceiptTemplateResponse template, int width)
        {
            var sample = new ReceiptDetailsResponse
            {
                // Номер образцовый: настоящий чек его получит из базы
                SaleId = 1001,
                SaleDate = DateTimeOffset.Now,
                CashierName = Tr.T("Receipt_TestCashier"),
                Subtotal = 260m,
                DiscountAmount = 10m,
                TotalAmount = 250m,
                PaidAmount = 250m,
                PaymentMethod = PaymentMethod.Cash,
                Lines =
                [
                    new ReceiptLineResponse
                    {
                        ProductName = Tr.T("Receipt_TestItem1"),
                        Barcode = "4870001234567",
                        Quantity = 2,
                        Price = 45m,
                    },
                    new ReceiptLineResponse
                    {
                        ProductName = Tr.T("Receipt_TestItem2"),
                        Barcode = "4870007654321",
                        Quantity = 1,
                        Price = 170m,
                    },
                ],
            };

            var lines = Render(
                template,
                sample,
                width,
                new CashInfo { CashGiven = 300m, Change = 50m });

            lines.Insert(0, ReceiptPrintLine.Of(Tr.T("Receipt_TestTitle"), PrintAlign.Center, bold: true));

            return lines;
        }

        // ===================== Вёрстка =====================

        private static void AddSeparator(List<ReceiptPrintLine> lines, int width)
            => lines.Add(ReceiptPrintLine.Of(new string('-', width)));

        /// <summary>
        /// Русская культура разделяет тысячи неразрывным пробелом (U+00A0).
        /// В CP866 это байт 0xFF, и термопринтеры печатают на его месте пустой
        /// квадрат — поэтому приводим к обычному пробелу.
        /// </summary>
        private static string Money(decimal value)
            => value.ToString("N2", CultureInfo.CurrentCulture)
                .Replace('\u00A0', ' ')
                .Replace('\u202F', ' ');

        /// <summary>
        /// Подпись слева, сумма справа. Если вместе не помещаются, сумма
        /// важнее — подпись обрезаем.
        /// </summary>
        private static string Columns(string left, string right, int width)
        {
            var space = width - right.Length - 1;

            if (space < 1)
                return right.PadLeft(width);

            if (left.Length > space)
                left = left[..space];

            return left + right.PadLeft(width - left.Length);
        }

        /// <summary>Переносит длинный текст по словам, чтобы не обрезать название товара.</summary>
        private static void AddWrapped(
            List<ReceiptPrintLine> lines,
            string text,
            int width,
            PrintAlign align = PrintAlign.Left)
        {
            foreach (var raw in text.Split('\n'))
            {
                var rest = raw.Trim();

                if (rest.Length == 0)
                {
                    lines.Add(ReceiptPrintLine.Empty());
                    continue;
                }

                while (rest.Length > width)
                {
                    // Рвём по последнему пробелу в пределах ширины; сплошную строку режем жёстко
                    var cut = rest.LastIndexOf(' ', Math.Min(width, rest.Length - 1));

                    if (cut <= 0)
                        cut = width;

                    lines.Add(ReceiptPrintLine.Of(rest[..cut].TrimEnd(), align));

                    rest = rest[cut..].TrimStart();
                }

                if (rest.Length > 0)
                    lines.Add(ReceiptPrintLine.Of(rest, align));
            }
        }

        private static string PaymentName(PaymentMethod method) => method switch
        {
            PaymentMethod.Cash => Tr.T("Payment_Cash"),
            PaymentMethod.Card => Tr.T("Payment_Card"),
            PaymentMethod.Transfer => Tr.T("Payment_Transfer"),
            PaymentMethod.Credit => Tr.T("Payment_Credit"),
            _ => Tr.T("Payment_Unknown"),
        };
    }
}
