namespace Application.DTOs.Printing
{
    public enum PrintAlign
    {
        Left,
        Center,
        Right
    }

    /// <summary>
    /// Одна строка ленты. Текст уже разбит по ширине принтера — драйверу
    /// остаётся только выставить начертание и выровнять.
    /// </summary>
    public class ReceiptPrintLine
    {
        public string Text { get; set; } = "";

        public PrintAlign Align { get; set; } = PrintAlign.Left;

        public bool Bold { get; set; }

        /// <summary>Двойная высота — для названия магазина и итога.</summary>
        public bool DoubleHeight { get; set; }

        public static ReceiptPrintLine Of(
            string text,
            PrintAlign align = PrintAlign.Left,
            bool bold = false,
            bool doubleHeight = false)
            => new() { Text = text, Align = align, Bold = bold, DoubleHeight = doubleHeight };

        /// <summary>Пустая строка-отступ.</summary>
        public static ReceiptPrintLine Empty() => new();
    }

    /// <summary>
    /// Готовый к печати чек: слой Application собирает строки, слой
    /// Infrastructure переводит их в команды принтера.
    /// </summary>
    public class ReceiptDocument
    {
        /// <summary>Пусто — печатаем на принтер Windows по умолчанию.</summary>
        public string PrinterName { get; set; } = "";

        public int Codepage { get; set; } = 866;

        public List<ReceiptPrintLine> Lines { get; set; } = new();

        public bool CutPaper { get; set; } = true;

        public bool OpenCashDrawer { get; set; }

        public int FeedLines { get; set; } = 3;

        /// <summary>Имя задания в очереди печати.</summary>
        public string JobName { get; set; } = "Receipt";
    }
}
