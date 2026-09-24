using System.Globalization;
using Application.DTOs.Labels;
using Application.DTOs.Printing;
using Application.Localization;

namespace Application.Services
{
    /// <summary>
    /// Собирает этикетку товара. Предпросмотр на экране и печать ходят сюда
    /// вместе — иначе наклейка расходилась бы с тем, что показали владельцу.
    /// </summary>
    public static class LabelRenderer
    {
        /// <summary>Строки одной этикетки, без обрезки в конце.</summary>
        public static List<ReceiptPrintLine> Render(
            LabelProductResponse product,
            LabelOptions options,
            int width)
        {
            var lines = new List<ReceiptPrintLine>();

            if (options.ShowName && product.Name.Length > 0)
                AddWrapped(lines, product.Name, width, PrintAlign.Center, bold: true);

            if (options.ShowPrice)
            {
                lines.Add(ReceiptPrintLine.Of(
                    Tr.F("Label_Price", product.PricePerUnit.ToString("N2", CultureInfo.CurrentCulture)
                        .Replace(' ', ' ')
                        .Replace(' ', ' ')),
                    PrintAlign.Center,
                    bold: true,
                    doubleHeight: true));
            }

            lines.Add(ReceiptPrintLine.Empty());

            // Сам штрихкод: цифры под полосами печатает принтер
            lines.Add(ReceiptPrintLine.Of(
                new ReceiptBarcode
                {
                    Data = (product.Barcode ?? "").Trim(),
                    Symbology = BarcodeSymbology.Ean13,
                    // Толщину узкой полосы ниже 3 опускать нельзя: при 203 dpi это
                    // 0,25 мм — около 76% от номинала EAN-13, ниже допустимых 80%,
                    // и дешёвые сканеры начинают промахиваться. Тройка даёт 0,375 мм
                    // и укладывается в 384 точки ленты 58 мм вместе с полями.
                    ModuleWidth = width >= 48 ? 4 : 3,
                    HeightDots = 80,
                    ShowDigits = true,
                },
                PrintAlign.Center));

            // Артикул печатаем, только если он отличается от кода: при приёмке
            // сканированием он равен штрихкоду, и строка была бы повтором цифр
            if (options.ShowSku
                && product.SKU.Length > 0
                && !string.Equals(product.SKU.Trim(), (product.Barcode ?? "").Trim(), StringComparison.Ordinal))
            {
                AddWrapped(lines, Tr.F("Label_Sku", product.SKU), width, PrintAlign.Center);
            }

            return lines;
        }

        /// <summary>Несколько этикеток подряд, каждая отрезается отдельно.</summary>
        public static List<ReceiptPrintLine> RenderSheet(
            LabelProductResponse product,
            LabelOptions options,
            int width)
        {
            var sheet = new List<ReceiptPrintLine>();

            var copies = Math.Clamp(options.Copies, 1, 100);

            for (var i = 0; i < copies; i++)
            {
                sheet.AddRange(Render(product, options, width));

                // Последнюю обрезает сам документ — иначе выедет лишний кусок
                if (i < copies - 1)
                    sheet.Add(ReceiptPrintLine.Cut());
            }

            return sheet;
        }

        private static void AddWrapped(
            List<ReceiptPrintLine> lines,
            string text,
            int width,
            PrintAlign align,
            bool bold = false)
        {
            foreach (var raw in text.Split('\n'))
            {
                var rest = raw.Trim();

                while (rest.Length > width)
                {
                    var cut = rest.LastIndexOf(' ', Math.Min(width, rest.Length - 1));

                    if (cut <= 0)
                        cut = width;

                    lines.Add(ReceiptPrintLine.Of(rest[..cut].TrimEnd(), align, bold));

                    rest = rest[cut..].TrimStart();
                }

                if (rest.Length > 0)
                    lines.Add(ReceiptPrintLine.Of(rest, align, bold));
            }
        }
    }
}
