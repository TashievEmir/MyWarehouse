using System.Runtime.InteropServices;
using System.Text;
using Application.Contracts.Interfaces;
using Application.DTOs.Printing;

namespace Infrastructure.Printing
{
    /// <summary>
    /// Печать чеков на термопринтер по ESC/POS через спулер Windows.
    ///
    /// Задание уходит в очередь с типом данных RAW: команды принтера идут в
    /// него как есть, минуя отрисовку драйвером. Только так доступны обрезка
    /// ленты, денежный ящик и однобайтные кодировки кириллицы — через обычную
    /// печать документа их не задать.
    ///
    /// Принтер должен быть установлен в Windows (драйвер производителя или
    /// совместимый POS-58 / Generic Text Only).
    /// </summary>
    public class EscPosReceiptPrinter : IReceiptPrinter
    {
        static EscPosReceiptPrinter()
        {
            // На .NET Core однобайтные кодировки (CP866, CP1251) отдельным пакетом
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        // ===================== ESC/POS =====================

        private const byte Esc = 0x1B;
        private const byte Gs = 0x1D;
        private const byte Fs = 0x1C;

        public Task PrintAsync(ReceiptDocument document, CancellationToken ct)
        {
            // Спулер синхронный, а вызывают нас из UI-потока кассы
            return Task.Run(() => Print(document), ct);
        }

        private void Print(ReceiptDocument document)
        {
            var printer = document.PrinterName;

            if (string.IsNullOrWhiteSpace(printer))
            {
                printer = GetDefaultPrinter()
                    ?? throw new InvalidOperationException(
                        "Принтер не выбран, а принтера по умолчанию в системе нет");
            }

            var bytes = Build(document);

            SendToPrinter(printer, document.JobName, bytes);
        }

        private static byte[] Build(ReceiptDocument document)
        {
            var encoding = GetEncoding(document.Codepage);

            using var stream = new MemoryStream();

            // Сброс: снимает настройки от предыдущего задания
            stream.Write([Esc, 0x40]);

            // Отмена иероглифического режима. На принтерах с китайской прошивкой
            // он включён по умолчанию: байты ≥0x80 читаются парами как двухбайтный
            // GBK, и кириллица печатается иероглифами. Однобайтную таблицу ниже
            // принтер начинает слушать только после этой команды.
            stream.Write([Fs, 0x2E]);

            // Таблица символов под кириллицу
            stream.Write([Esc, 0x74, CodepageCommand(document.Codepage)]);

            var bold = false;
            var doubleHeight = false;
            var align = PrintAlign.Left;

            foreach (var line in document.Lines)
            {
                if (line.Align != align)
                {
                    align = line.Align;
                    stream.Write([Esc, 0x61, (byte)align]);
                }

                if (line.Bold != bold)
                {
                    bold = line.Bold;
                    stream.Write([Esc, 0x45, (byte)(bold ? 1 : 0)]);
                }

                if (line.DoubleHeight != doubleHeight)
                {
                    doubleHeight = line.DoubleHeight;
                    stream.Write([Gs, 0x21, (byte)(doubleHeight ? 0x01 : 0x00)]);
                }

                if (line.Barcode is { } barcode)
                {
                    WriteBarcode(stream, barcode);
                }
                else
                {
                    stream.Write(encoding.GetBytes(line.Text));
                    stream.Write([(byte)'\n']);
                }

                // Конец этикетки: промотка и обрез посреди задания
                if (line.CutAfter)
                {
                    stream.Write([Esc, 0x64, (byte)Math.Max(document.FeedLines, 1)]);
                    stream.Write([Gs, 0x56, 0x42, 0x00]);
                }
            }

            // Возврат к обычному начертанию, иначе следующий чек унаследует стиль
            stream.Write([Esc, 0x45, 0x00]);
            stream.Write([Gs, 0x21, 0x00]);
            stream.Write([Esc, 0x61, 0x00]);

            if (document.FeedLines > 0)
                stream.Write([Esc, 0x64, (byte)document.FeedLines]);

            if (document.CutPaper)
                stream.Write([Gs, 0x56, 0x42, 0x00]);

            // Ящик открываем после обрезки: касса уже отдала чек
            if (document.OpenCashDrawer)
                stream.Write([Esc, 0x70, 0x00, 0x19, 0xFA]);

            return stream.ToArray();
        }

        /// <summary>
        /// Штрихкод рисует сам принтер. Нас интересует семейство команд с явной
        /// длиной данных («GS k m n …»): в отличие от варианта с завершающим
        /// нулём оно не спотыкается, если в данных попадётся нулевой байт.
        /// </summary>
        private static void WriteBarcode(Stream stream, ReceiptBarcode barcode)
        {
            var data = (barcode.Data ?? "").Trim();

            if (data.Length == 0)
                return;

            // Цифры под полосами: 0 — нет, 2 — снизу
            stream.Write([Gs, 0x48, (byte)(barcode.ShowDigits ? 2 : 0)]);

            // Шрифт цифр: мелкий, иначе на узкой ленте они спорят с названием
            stream.Write([Gs, 0x66, 0x01]);

            stream.Write([Gs, 0x68, (byte)Math.Clamp(barcode.HeightDots, 1, 255)]);
            stream.Write([Gs, 0x77, (byte)Math.Clamp(barcode.ModuleWidth, 2, 6)]);

            var type = barcode.Symbology switch
            {
                BarcodeSymbology.Ean13 => (byte)67,
                _ => (byte)67,
            };

            var payload = Encoding.ASCII.GetBytes(data);

            stream.Write([Gs, 0x6B, type, (byte)payload.Length]);
            stream.Write(payload);

            // Принтер не переводит строку сам после штрихкода
            stream.Write([(byte)'\n']);
        }

        private static Encoding GetEncoding(int codepage)
        {
            // Кодировка может отсутствовать в системе — печатаем хотя бы латиницей,
            // это лучше, чем уронить кассу на закрытии сделки
            try
            {
                return Encoding.GetEncoding(codepage);
            }
            catch (Exception)
            {
                return Encoding.ASCII;
            }
        }

        /// <summary>
        /// Номер таблицы символов для «ESC t n».
        ///
        /// Нумерация у ESC/POS расходится между производителями: стандарт Epson
        /// держит CP866 на 17 и CP1251 на 73, а массовые китайские прошивки
        /// (SPRT, XPrinter, Gprinter) — на 7 и 6 соответственно.
        ///
        /// Значения ниже проверены печатью на SPRT SP-POS581X: таблицы 6 и 7
        /// дали читаемую кириллицу, 17 и 73 — мусор. Если появится принтер с
        /// нумерацией Epson, кириллица снова поедет — тогда номера надо
        /// вынести в настройки рядом с выбором кодировки.
        /// </summary>
        private static byte CodepageCommand(int codepage) => codepage switch
        {
            437 => 0,
            866 => 7,
            1251 => 6,
            _ => 7,
        };

        // ===================== Спулер Windows =====================

        /// <summary>PRINTER_INFO_4 — самый дешёвый уровень, отдаёт только имена.</summary>
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct PrinterInfo4
        {
            [MarshalAs(UnmanagedType.LPWStr)] public string pPrinterName;
            [MarshalAs(UnmanagedType.LPWStr)] public string pServerName;
            public int Attributes;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct DocInfo
        {
            [MarshalAs(UnmanagedType.LPWStr)] public string pDocName;
            [MarshalAs(UnmanagedType.LPWStr)] public string? pOutputFile;
            [MarshalAs(UnmanagedType.LPWStr)] public string pDataType;
        }

        [DllImport("winspool.drv", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool OpenPrinter(string src, out IntPtr hPrinter, IntPtr pd);

        [DllImport("winspool.drv", SetLastError = true)]
        private static extern bool ClosePrinter(IntPtr hPrinter);

        [DllImport("winspool.drv", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool StartDocPrinter(IntPtr hPrinter, int level, ref DocInfo di);

        [DllImport("winspool.drv", SetLastError = true)]
        private static extern bool EndDocPrinter(IntPtr hPrinter);

        [DllImport("winspool.drv", SetLastError = true)]
        private static extern bool StartPagePrinter(IntPtr hPrinter);

        [DllImport("winspool.drv", SetLastError = true)]
        private static extern bool EndPagePrinter(IntPtr hPrinter);

        [DllImport("winspool.drv", SetLastError = true)]
        private static extern bool WritePrinter(IntPtr hPrinter, IntPtr pBytes, int count, out int written);

        [DllImport("winspool.drv", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool GetDefaultPrinter(StringBuilder? buffer, ref int size);

        [DllImport("winspool.drv", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool EnumPrinters(
            int flags,
            string? name,
            int level,
            IntPtr pPrinterEnum,
            int cbBuf,
            out int pcbNeeded,
            out int pcReturned);

        private const int PrinterEnumLocal = 0x00000002;
        private const int PrinterEnumConnections = 0x00000004;

        private static void SendToPrinter(string printerName, string jobName, byte[] bytes)
        {
            if (!OpenPrinter(printerName, out var handle, IntPtr.Zero))
                throw BuildError($"Не удалось открыть принтер «{printerName}»");

            var unmanaged = IntPtr.Zero;

            try
            {
                var info = new DocInfo
                {
                    pDocName = jobName,
                    pOutputFile = null,
                    // RAW — команды уходят на принтер без обработки драйвером
                    pDataType = "RAW",
                };

                if (!StartDocPrinter(handle, 1, ref info))
                    throw BuildError("Не удалось создать задание печати");

                try
                {
                    if (!StartPagePrinter(handle))
                        throw BuildError("Не удалось начать страницу");

                    try
                    {
                        unmanaged = Marshal.AllocCoTaskMem(bytes.Length);

                        Marshal.Copy(bytes, 0, unmanaged, bytes.Length);

                        if (!WritePrinter(handle, unmanaged, bytes.Length, out var written)
                            || written != bytes.Length)
                        {
                            throw BuildError("Не удалось передать данные принтеру");
                        }
                    }
                    finally
                    {
                        EndPagePrinter(handle);
                    }
                }
                finally
                {
                    EndDocPrinter(handle);
                }
            }
            finally
            {
                if (unmanaged != IntPtr.Zero)
                    Marshal.FreeCoTaskMem(unmanaged);

                ClosePrinter(handle);
            }
        }

        private static InvalidOperationException BuildError(string message)
            => new($"{message}: {new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error()).Message}");

        public string? GetDefaultPrinter()
        {
            var size = 0;

            GetDefaultPrinter(null, ref size);

            if (size <= 0)
                return null;

            var buffer = new StringBuilder(size);

            return GetDefaultPrinter(buffer, ref size) ? buffer.ToString() : null;
        }

        public IReadOnlyList<string> GetInstalledPrinters()
        {
            const int flags = PrinterEnumLocal | PrinterEnumConnections;
            const int level = 4;

            // Первый вызов только узнаёт нужный размер буфера
            EnumPrinters(flags, null, level, IntPtr.Zero, 0, out var needed, out _);

            if (needed <= 0)
                return [];

            var buffer = Marshal.AllocHGlobal(needed);

            try
            {
                if (!EnumPrinters(flags, null, level, buffer, needed, out _, out var count) || count == 0)
                    return [];

                var result = new List<string>(count);

                // Размер берём у самой структуры: на x64 она выровнена до 24 байт,
                // а не до суммы полей — ручной подсчёт съезжал бы по записям
                var entrySize = Marshal.SizeOf<PrinterInfo4>();

                for (var i = 0; i < count; i++)
                {
                    var entry = Marshal.PtrToStructure<PrinterInfo4>(buffer + i * entrySize);

                    if (!string.IsNullOrWhiteSpace(entry.pPrinterName))
                        result.Add(entry.pPrinterName);
                }

                return result;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
    }
}
