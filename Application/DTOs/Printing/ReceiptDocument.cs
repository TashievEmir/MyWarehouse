namespace Application.DTOs.Printing
{
    public enum PrintAlign
    {
        Left,
        Center,
        Right
    }

    public enum BarcodeSymbology
    {
        /// <summary>13 цифр с контрольной — тот же формат, что у заводских кодов.</summary>
        Ean13
    }

    /// <summary>
    /// Штрихкод в ленте. Рисует его сам принтер по команде ESC/POS — так полосы
    /// выходят ровными при любой ширине ленты, в отличие от печати картинкой.
    /// </summary>
    public class ReceiptBarcode
    {
        public string Data { get; set; } = "";

        public BarcodeSymbology Symbology { get; set; } = BarcodeSymbology.Ean13;

        /// <summary>Высота полос в точках. 80 ≈ 10 мм — сканер берёт уверенно.</summary>
        public int HeightDots { get; set; } = 80;

        /// <summary>Толщина узкой полосы, 2–6. На ленте 58 мм EAN-13 помещается при 3.</summary>
        public int ModuleWidth { get; set; } = 3;

        /// <summary>Печатать цифры кода под полосами — их вводят руками, если код не считался.</summary>
        public bool ShowDigits { get; set; } = true;
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

        /// <summary>Задан — вместо текста печатается штрихкод.</summary>
        public ReceiptBarcode? Barcode { get; set; }

        /// <summary>
        /// Обрезать ленту после этой строки. Нужно для этикеток: они печатаются
        /// одним заданием, но каждая должна выйти отдельным кусочком.
        /// </summary>
        public bool CutAfter { get; set; }

        public static ReceiptPrintLine Of(
            string text,
            PrintAlign align = PrintAlign.Left,
            bool bold = false,
            bool doubleHeight = false)
            => new() { Text = text, Align = align, Bold = bold, DoubleHeight = doubleHeight };

        public static ReceiptPrintLine Of(ReceiptBarcode barcode, PrintAlign align = PrintAlign.Center)
            => new() { Barcode = barcode, Align = align };

        /// <summary>Пустая строка-отступ.</summary>
        public static ReceiptPrintLine Empty() => new();

        /// <summary>Промотка и обрез — конец этикетки.</summary>
        public static ReceiptPrintLine Cut() => new() { CutAfter = true };
    }

    /// <summary>
    /// Готовый к печати документ: слой Application собирает строки, слой
    /// Infrastructure переводит их в команды принтера.
    /// </summary>
    public class ReceiptDocument
    {
        /// <summary>Пусто — печатаем на принтер Windows по умолчанию.</summary>
        public string PrinterName { get; set; } = "";

        public int Codepage { get; set; } = 866;

        public List<ReceiptPrintLine> Lines { get; set; } = new();

        /// <summary>Обрезать в конце документа. Промежуточные обрезы задаёт сама строка.</summary>
        public bool CutPaper { get; set; } = true;

        public bool OpenCashDrawer { get; set; }

        public int FeedLines { get; set; } = 3;

        /// <summary>Имя задания в очереди печати.</summary>
        public string JobName { get; set; } = "Receipt";
    }
}
