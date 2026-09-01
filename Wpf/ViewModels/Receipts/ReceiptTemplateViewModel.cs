using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using Application.Contracts.Interfaces;
using Application.DTOs.Printing;
using Application.DTOs.Receipts;
using Application.Services;
using Wpf.Common;
using Wpf.Services;

using Wpf.Localization;

namespace Wpf.ViewModels.Receipts;

/// <summary>Блок чека в редакторе.</summary>
public class ReceiptBlockItem : ViewModelBase
{
    public string Key { get; }
    public string Name { get; }
    public string Hint { get; }
    public bool IsLocked { get; }

    /// <summary>Обязательный блок не переключается.</summary>
    public bool CanToggle => !IsLocked;

    private bool _isEnabled;
    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            // Обязательный блок выключить нельзя
            if (IsLocked)
                value = true;

            SetProperty(ref _isEnabled, value);
        }
    }

    public ReceiptBlockItem(ReceiptBlockResponse block)
    {
        Key = block.Key;
        Name = block.Name;
        Hint = block.Hint;
        IsLocked = block.IsLocked;
        _isEnabled = block.IsEnabled;
    }

    public ReceiptBlockState ToState() => new() { Key = Key, IsEnabled = IsEnabled };
}

/// <summary>Вариант выбора в выпадающем списке настроек принтера.</summary>
public class PrinterChoice
{
    public string Title { get; }
    public string Value { get; }

    public PrinterChoice(string title, string value)
    {
        Title = title;
        Value = value;
    }
}

/// <summary>Числовой вариант: ширина ленты и кодировка.</summary>
public class PrinterNumberChoice
{
    public string Title { get; }
    public int Value { get; }

    public PrinterNumberChoice(string title, int value)
    {
        Title = title;
        Value = value;
    }
}

/// <summary>
/// Редактор шаблона чека: шапка, порядок блоков и подвал, плюс настройки
/// принтера. Любое изменение сразу перерисовывает предпросмотр ленты.
/// </summary>
public class ReceiptTemplateViewModel : ViewModelBase
{
    private readonly IReceiptTemplateService _templates;
    private readonly IPrinterSettingsService _printerSettings;
    private readonly IReceiptPrintService _printing;
    private readonly IReceiptPrinter _printer;
    private readonly SessionService _session;

    public ObservableCollection<ReceiptBlockItem> Blocks { get; } = new();

    /// <summary>Принтеры, установленные в Windows, плюс вариант «по умолчанию».</summary>
    public ObservableCollection<PrinterChoice> Printers { get; } = new();

    public ObservableCollection<PrinterNumberChoice> Widths { get; } = new();

    public ObservableCollection<PrinterNumberChoice> Codepages { get; } = new();

    public ICommand SaveCommand { get; }
    public ICommand MoveUpCommand { get; }
    public ICommand MoveDownCommand { get; }
    public ICommand TestPrintCommand { get; }

    public ReceiptTemplateViewModel(
        IReceiptTemplateService templates,
        IPrinterSettingsService printerSettings,
        IReceiptPrintService printing,
        IReceiptPrinter printer,
        SessionService session)
    {
        _templates = templates;
        _printerSettings = printerSettings;
        _printing = printing;
        _printer = printer;
        _session = session;

        SaveCommand = new AsyncRelayCommand(SaveAsync);
        MoveUpCommand = new RelayCommand<ReceiptBlockItem>(block => Move(block, -1));
        MoveDownCommand = new RelayCommand<ReceiptBlockItem>(block => Move(block, +1));
        TestPrintCommand = new AsyncRelayCommand(TestPrintAsync);

        Blocks.CollectionChanged += (_, __) => RefreshPreview();
    }

    // ===================== Поля шапки =====================

    private string _shopName = "";
    public string ShopName
    {
        get => _shopName;
        set { if (SetProperty(ref _shopName, value)) RefreshPreview(); }
    }

    private string _tin = "";
    public string Tin
    {
        get => _tin;
        set { if (SetProperty(ref _tin, value)) RefreshPreview(); }
    }

    private string _address = "";
    public string Address
    {
        get => _address;
        set { if (SetProperty(ref _address, value)) RefreshPreview(); }
    }

    private string _footerText = "";
    public string FooterText
    {
        get => _footerText;
        set { if (SetProperty(ref _footerText, value)) RefreshPreview(); }
    }

    public string RoleChip => _session.User?.Roles.FirstOrDefault() switch
    {
        "Admin"   => Loc.T("Template_Role_Admin"),
        "Manager" => Loc.T("Template_Role_Manager"),
        _         => Loc.T("Template_Role_Viewer"),
    };

    // ===================== Настройки принтера =====================

    private bool _autoPrint;
    /// <summary>Печатать чек сразу после закрытия сделки.</summary>
    public bool AutoPrint
    {
        get => _autoPrint;
        set => SetProperty(ref _autoPrint, value);
    }

    private PrinterChoice? _selectedPrinter;
    public PrinterChoice? SelectedPrinter
    {
        get => _selectedPrinter;
        set => SetProperty(ref _selectedPrinter, value);
    }

    private PrinterNumberChoice? _selectedWidth;
    public PrinterNumberChoice? SelectedWidth
    {
        get => _selectedWidth;
        set
        {
            // Ширина ленты меняет вёрстку — предпросмотр должен это показать
            if (!SetProperty(ref _selectedWidth, value))
                return;

            OnPropertyChanged(nameof(PreviewHint));

            RefreshPreview();
        }
    }

    /// <summary>Подпись над предпросмотром: показывает выбранную ширину ленты.</summary>
    public string PreviewHint => Loc.F("Template_PreviewHint", SelectedWidth?.Title ?? "");

    private PrinterNumberChoice? _selectedCodepage;
    public PrinterNumberChoice? SelectedCodepage
    {
        get => _selectedCodepage;
        set => SetProperty(ref _selectedCodepage, value);
    }

    private bool _cutPaper = true;
    public bool CutPaper
    {
        get => _cutPaper;
        set => SetProperty(ref _cutPaper, value);
    }

    private bool _openCashDrawer;
    public bool OpenCashDrawer
    {
        get => _openCashDrawer;
        set => SetProperty(ref _openCashDrawer, value);
    }

    private int _feedLines = 3;
    public int FeedLines
    {
        get => _feedLines;
        set => SetProperty(ref _feedLines, Math.Clamp(value, 0, 10));
    }

    /// <summary>Ширина ленты в символах — её же использует предпросмотр.</summary>
    private int Width => SelectedWidth?.Value ?? Domain.Entities.PrinterSettings.DefaultCharsPerLine;

    private void FillPrinterChoices()
    {
        Widths.Clear();
        Widths.Add(new PrinterNumberChoice(Loc.T("Printer_Width58"), 32));
        Widths.Add(new PrinterNumberChoice(Loc.T("Printer_Width80"), 48));

        Codepages.Clear();
        Codepages.Add(new PrinterNumberChoice("CP866", 866));
        Codepages.Add(new PrinterNumberChoice("CP1251", 1251));

        Printers.Clear();
        Printers.Add(new PrinterChoice(Loc.T("Printer_UseDefault"), ""));

        // Список принтеров — вопрос к системе: она может и не ответить
        try
        {
            foreach (var name in _printer.GetInstalledPrinters())
                Printers.Add(new PrinterChoice(name, name));
        }
        catch
        {
            // оставляем только «по умолчанию»
        }
    }

    private async Task TestPrintAsync()
    {
        if (Printers.Count <= 1)
        {
            ShowError(Loc.T("Printer_NoPrinters"));
            return;
        }

        if (_session.User is null)
        {
            ShowError(Loc.T("Template_NoLogin"));
            return;
        }

        IsBusy = true;

        try
        {
            // Печатаем то, что сейчас в полях: настройки сперва сохраняем
            await SavePrinterSettingsAsync();

            await _printing.PrintTestAsync(CancellationToken.None);

            ShowInfo(Loc.T("Printer_TestOk"));
        }
        catch (Exception ex)
        {
            ShowError(Loc.F("Printer_TestFailed", ex.Message));
        }
        finally
        {
            IsBusy = false;
        }
    }

    private Task SavePrinterSettingsAsync()
    {
        return _printerSettings.SaveAsync(new SavePrinterSettingsRequest
        {
            UserId         = _session.User!.UserId,
            AutoPrint      = AutoPrint,
            PrinterName    = SelectedPrinter?.Value ?? "",
            CharsPerLine   = Width,
            Codepage       = SelectedCodepage?.Value ?? Domain.Entities.PrinterSettings.DefaultCodepage,
            CutPaper       = CutPaper,
            OpenCashDrawer = OpenCashDrawer,
            FeedLines      = FeedLines,
        }, CancellationToken.None);
    }

    // ===================== Предпросмотр =====================

    private string _preview = "";
    public string Preview
    {
        get => _preview;
        private set => SetProperty(ref _preview, value);
    }

    /// <summary>
    /// Предпросмотр собирается тем же рендером, что и печать, — на экране
    /// ровно та лента, которая выйдет из принтера. Выравнивание принтер
    /// делает сам, поэтому для экрана его приходится доигрывать пробелами.
    /// </summary>
    private void RefreshPreview()
    {
        var template = new ReceiptTemplateResponse
        {
            ShopName   = ShopName,
            Tin        = Tin,
            Address    = Address,
            FooterText = FooterText,
            Blocks = Blocks
                .Select(b => new ReceiptBlockResponse { Key = b.Key, IsEnabled = b.IsEnabled })
                .ToList(),
        };

        var width = Width;

        var lines = ReceiptRenderer.RenderTest(template, width);

        Preview = string.Join(Environment.NewLine, lines.Select(line => Pad(line, width)));
    }

    private static string Pad(ReceiptPrintLine line, int width) => line.Align switch
    {
        PrintAlign.Center => line.Text.Length >= width
            ? line.Text
            : line.Text.PadLeft((width + line.Text.Length) / 2),
        PrintAlign.Right => line.Text.PadLeft(width),
        _ => line.Text,
    };

    // ===================== Порядок блоков =====================

    private void Move(ReceiptBlockItem block, int offset)
    {
        var index = Blocks.IndexOf(block);
        var target = index + offset;

        if (index < 0 || target < 0 || target >= Blocks.Count)
            return;

        Blocks.Move(index, target);
    }

    // ===================== Загрузка и сохранение =====================

    public async Task LoadAsync()
    {
        IsBusy = true;

        try
        {
            FillPrinterChoices();

            await LoadPrinterSettingsAsync();

            var template = await _templates.GetAsync(CancellationToken.None);

            ShopName = template.ShopName;
            Tin = template.Tin ?? "";
            Address = template.Address ?? "";
            FooterText = template.FooterText ?? "";

            foreach (var block in Blocks)
                block.PropertyChanged -= OnBlockChanged;

            Blocks.Clear();

            foreach (var block in template.Blocks)
            {
                var item = new ReceiptBlockItem(block);

                item.PropertyChanged += OnBlockChanged;

                Blocks.Add(item);
            }

            StatusMessage = "";

            OnPropertyChanged(nameof(RoleChip));

            RefreshPreview();
        }
        catch (Exception ex)
        {
            ShowError(Loc.F("Template_LoadFailed", ex.Message));
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void OnBlockChanged(object? sender, PropertyChangedEventArgs e) => RefreshPreview();

    private async Task LoadPrinterSettingsAsync()
    {
        try
        {
            var settings = await _printerSettings.GetAsync(CancellationToken.None);

            AutoPrint      = settings.AutoPrint;
            CutPaper       = settings.CutPaper;
            OpenCashDrawer = settings.OpenCashDrawer;
            FeedLines      = settings.FeedLines;

            // Сохранённого принтера может уже не быть в системе — тогда «по умолчанию»
            SelectedPrinter = Printers.FirstOrDefault(p => p.Value == settings.PrinterName)
                              ?? Printers.FirstOrDefault();

            SelectedWidth    = Widths.FirstOrDefault(w => w.Value == settings.CharsPerLine) ?? Widths.FirstOrDefault();
            SelectedCodepage = Codepages.FirstOrDefault(c => c.Value == settings.Codepage) ?? Codepages.FirstOrDefault();
        }
        catch (Exception ex)
        {
            ShowError(Loc.F("Printer_LoadFailed", ex.Message));
        }
    }

    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(ShopName))
        {
            ShowError(Loc.T("Template_NeedShopName"));
            return;
        }

        if (_session.User is null)
        {
            ShowError(Loc.T("Template_NoLogin"));
            return;
        }

        IsBusy = true;

        try
        {
            await _templates.SaveAsync(new SaveReceiptTemplateRequest
            {
                UserId     = _session.User.UserId,
                ShopName   = ShopName,
                Tin        = Tin,
                Address    = Address,
                FooterText = FooterText,
                Blocks     = Blocks.Select(b => b.ToState()).ToList(),
            }, CancellationToken.None);

            await SavePrinterSettingsAsync();

            ShowInfo(Loc.T("Template_Saved"));
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    // ===================== Состояние =====================

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    private string _statusMessage = "";
    public string StatusMessage
    {
        get => _statusMessage;
        private set
        {
            if (SetProperty(ref _statusMessage, value))
                OnPropertyChanged(nameof(HasStatus));
        }
    }

    private bool _statusIsError;
    public bool StatusIsError
    {
        get => _statusIsError;
        private set => SetProperty(ref _statusIsError, value);
    }

    public bool HasStatus => StatusMessage.Length > 0;

    private void ShowInfo(string message)
    {
        StatusIsError = false;
        StatusMessage = message;
    }

    private void ShowError(string message)
    {
        StatusIsError = true;
        StatusMessage = message;
    }
}
