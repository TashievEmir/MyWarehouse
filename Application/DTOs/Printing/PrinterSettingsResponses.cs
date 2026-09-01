namespace Application.DTOs.Printing
{
    public class PrinterSettingsResponse
    {
        public bool AutoPrint { get; set; }

        public string PrinterName { get; set; } = "";

        public int CharsPerLine { get; set; } = 32;

        public int Codepage { get; set; } = 866;

        public bool CutPaper { get; set; } = true;

        public bool OpenCashDrawer { get; set; }

        public int FeedLines { get; set; } = 3;
    }

    public class SavePrinterSettingsRequest
    {
        public long UserId { get; set; }

        public bool AutoPrint { get; set; }

        public string PrinterName { get; set; } = "";

        public int CharsPerLine { get; set; } = 32;

        public int Codepage { get; set; } = 866;

        public bool CutPaper { get; set; } = true;

        public bool OpenCashDrawer { get; set; }

        public int FeedLines { get; set; } = 3;
    }
}
