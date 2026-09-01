using Domain.Exceptions;

namespace Domain.Entities
{
    /// <summary>
    /// Настройки чекового принтера. В базе одна строка — как у шаблона чека
    /// и почтовых уведомлений.
    /// </summary>
    public class PrinterSettings
    {
        /// <summary>Лента 58 мм — 32 символа в строке, 80 мм — 48.</summary>
        public const int DefaultCharsPerLine = 32;

        /// <summary>Кириллица на дешёвых ESC/POS принтерах чаще всего идёт в CP866.</summary>
        public const int DefaultCodepage = 866;

        public long Id { get; private set; }

        /// <summary>Печатать чек сразу после закрытия сделки.</summary>
        public bool AutoPrint { get; private set; }

        /// <summary>Имя принтера в Windows. Пусто — берём принтер по умолчанию.</summary>
        public string PrinterName { get; private set; } = "";

        public int CharsPerLine { get; private set; } = DefaultCharsPerLine;

        public int Codepage { get; private set; } = DefaultCodepage;

        /// <summary>Обрезать ленту после чека.</summary>
        public bool CutPaper { get; private set; } = true;

        /// <summary>Открывать денежный ящик при оплате наличными.</summary>
        public bool OpenCashDrawer { get; private set; }

        /// <summary>Сколько строк промотать перед обрезкой, чтобы чек вышел из-под ножа.</summary>
        public int FeedLines { get; private set; } = 3;

        public DateTimeOffset UpdatedAt { get; private set; }

        private PrinterSettings() { }

        public PrinterSettings(
            bool autoPrint,
            string printerName,
            int charsPerLine,
            int codepage,
            bool cutPaper,
            bool openCashDrawer,
            int feedLines)
        {
            Update(autoPrint, printerName, charsPerLine, codepage, cutPaper, openCashDrawer, feedLines);
        }

        public void Update(
            bool autoPrint,
            string printerName,
            int charsPerLine,
            int codepage,
            bool cutPaper,
            bool openCashDrawer,
            int feedLines)
        {
            // Ширину и кодировку задаёт выпадающий список, но запрос может прийти и мимо интерфейса
            if (charsPerLine is < 24 or > 64)
                throw new DomainException("Ширина ленты вне допустимого диапазона");

            if (feedLines is < 0 or > 10)
                throw new DomainException("Промотка вне допустимого диапазона");

            AutoPrint = autoPrint;
            PrinterName = (printerName ?? "").Trim();
            CharsPerLine = charsPerLine;
            Codepage = codepage;
            CutPaper = cutPaper;
            OpenCashDrawer = openCashDrawer;
            FeedLines = feedLines;
            UpdatedAt = DateTimeOffset.UtcNow;
        }
    }
}
