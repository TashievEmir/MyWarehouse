using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using System.Windows.Input;
using Application.Contracts.Interfaces;
using Application.DTOs.Labels;
using Application.DTOs.Printing;
using Application.Services;
using Wpf.Common;
using Wpf.Localization;
using Wpf.Services;

namespace Wpf.ViewModels.Labels;

/// <summary>Товар в списке маркировки.</summary>
public class LabelProductItem
{
    public long ProductId { get; }
    public string Name { get; }
    public string SKU { get; }
    public string Barcode { get; }
    public string CategoryName { get; }
    public decimal Price { get; }
    public int InStock { get; }

    public bool HasBarcode => Barcode.Length > 0;

    /// <summary>Код выдан нами — его видно по метке в списке.</summary>
    public bool IsInternal { get; }

    public string BarcodeText => HasBarcode ? Barcode : Loc.T("Labels_NoBarcode");

    public string PriceText => Price.ToString("N2", Loc.Instance.Culture);

    public LabelProductItem(LabelProductResponse product)
    {
        ProductId = product.ProductId;
        Name = product.Name;
        SKU = product.SKU;
        Barcode = product.Barcode ?? "";
        CategoryName = product.CategoryName;
        Price = product.PricePerUnit;
        InStock = product.InStock;
        IsInternal = product.IsInternalBarcode;
    }

    public LabelProductResponse ToResponse() => new()
    {
        ProductId = ProductId,
        Name = Name,
        SKU = SKU,
        Barcode = Barcode,
        CategoryName = CategoryName,
        PricePerUnit = Price,
        InStock = InStock,
        IsInternalBarcode = IsInternal,
    };
}

/// <summary>
/// Штрихкоды и этикетки: слева список товаров, справа карточка с кодом,
/// предпросмотром наклейки и печатью.
///
/// Нужна, когда товар приехал без заводского кода: ему выдаётся внутренний
/// код того же формата EAN-13, печатается этикетка и клеится на упаковку.
/// </summary>
public class LabelsViewModel : ViewModelBase
{
    private readonly ILabelService _labels;
    private readonly IPrinterSettingsService _printerSettings;
    private readonly SessionService _session;

    public ObservableCollection<LabelProductItem> Items { get; } = new();

    public ICommand RefreshCommand { get; }
    public ICommand SelectCommand { get; }
    public ICommand GenerateCommand { get; }
    public ICommand SaveBarcodeCommand { get; }
    public ICommand PrintCommand { get; }
    public ICommand ClearSearchCommand { get; }
    public ICommand CloseCommand { get; }

    public LabelsViewModel(
        ILabelService labels,
        IPrinterSettingsService printerSettings,
        SessionService session)
    {
        _labels = labels;
        _printerSettings = printerSettings;
        _session = session;

        RefreshCommand = new AsyncRelayCommand(LoadAsync);
        SelectCommand = new RelayCommand<LabelProductItem>(Select);
        GenerateCommand = new AsyncRelayCommand(GenerateAsync);
        SaveBarcodeCommand = new AsyncRelayCommand(SaveBarcodeAsync);
        PrintCommand = new AsyncRelayCommand(PrintAsync);
        ClearSearchCommand = new RelayCommand(() => SearchText = "");
        CloseCommand = new RelayCommand(CloseEditor);

        Loc.LanguageChanged += () =>
        {
            OnPropertyChanged(string.Empty);
            _ = LoadAsync();
        };
    }

    // ===================== Список =====================

    private string _searchText = "";
    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                OnPropertyChanged(nameof(HasSearch));
                _ = LoadAsync();
            }
        }
    }

    public bool HasSearch => SearchText.Length > 0;

    private bool _onlyWithoutBarcode = true;
    /// <summary>По умолчанию показываем именно то, ради чего страница и нужна.</summary>
    public bool OnlyWithoutBarcode
    {
        get => _onlyWithoutBarcode;
        set
        {
            if (SetProperty(ref _onlyWithoutBarcode, value))
                _ = LoadAsync();
        }
    }

    private bool _isEmpty = true;
    public bool IsEmpty
    {
        get => _isEmpty;
        private set => SetProperty(ref _isEmpty, value);
    }

    public string EmptyText => OnlyWithoutBarcode
        ? Loc.T("Labels_EmptyNoneMissing")
        : Loc.T("Labels_EmptyNoProducts");

    public string CountText => Items.Count == 0 ? "" : Loc.F("Labels_Count", Items.Count);

    // ===================== Карточка =====================

    private LabelProductItem? _selected;
    public LabelProductItem? Selected
    {
        get => _selected;
        private set
        {
            if (SetProperty(ref _selected, value))
            {
                OnPropertyChanged(nameof(HasEditor));
                OnPropertyChanged(nameof(CanPrint));
                OnPropertyChanged(nameof(CanGenerate));
            }
        }
    }

    public bool HasEditor => Selected is not null;

    private string _barcodeInput = "";
    /// <summary>Код в карточке: можно переписать вручную с упаковки.</summary>
    public string BarcodeInput
    {
        get => _barcodeInput;
        set
        {
            if (SetProperty(ref _barcodeInput, value))
            {
                OnPropertyChanged(nameof(BarcodeHint));
                OnPropertyChanged(nameof(IsBarcodeValid));
                OnPropertyChanged(nameof(CanPrint));

                RefreshPreview();
            }
        }
    }

    public bool IsBarcodeValid => BarcodeGenerator.IsValid(BarcodeInput);

    /// <summary>Подсказка под полем: пусто, не тот формат или всё в порядке.</summary>
    public string BarcodeHint
    {
        get
        {
            var code = BarcodeInput.Trim();

            if (code.Length == 0)
                return Loc.T("Labels_HintEmpty");

            if (!BarcodeGenerator.IsValid(code))
                return Loc.T("Labels_HintInvalid");

            return BarcodeGenerator.IsInternal(code)
                ? Loc.T("Labels_HintInternal")
                : Loc.T("Labels_HintFactory");
        }
    }

    public bool CanGenerate => Selected is not null;

    public bool CanPrint => Selected is not null && IsBarcodeValid && !IsBusy;

    private void Select(LabelProductItem item)
    {
        if (item is null)
            return;

        Selected = item;
        BarcodeInput = item.Barcode;

        StatusMessage = "";

        RefreshPreview();
    }

    private void CloseEditor()
    {
        Selected = null;
        BarcodeInput = "";
        Preview = "";
        StatusMessage = "";
    }

    // ===================== Этикетка =====================

    private bool _showName = true;
    public bool ShowName
    {
        get => _showName;
        set { if (SetProperty(ref _showName, value)) RefreshPreview(); }
    }

    private bool _showPrice = true;
    public bool ShowPrice
    {
        get => _showPrice;
        set { if (SetProperty(ref _showPrice, value)) RefreshPreview(); }
    }

    private bool _showSku = true;
    public bool ShowSku
    {
        get => _showSku;
        set { if (SetProperty(ref _showSku, value)) RefreshPreview(); }
    }

    private int _copies = 1;
    public int Copies
    {
        get => _copies;
        set => SetProperty(ref _copies, Math.Clamp(value, 1, 100));
    }

    private LabelOptions BuildOptions() => new()
    {
        ShowName = ShowName,
        ShowPrice = ShowPrice,
        ShowSku = ShowSku,
        Copies = Copies,
    };

    private string _preview = "";
    /// <summary>Как этикетка ляжет на ленту — тем же рендером, что и печать.</summary>
    public string Preview
    {
        get => _preview;
        private set => SetProperty(ref _preview, value);
    }

    /// <summary>Ширина ленты из настроек принтера; до загрузки считаем 58 мм.</summary>
    private int _width = 32;

    private void RefreshPreview()
    {
        if (Selected is null)
        {
            Preview = "";
            return;
        }

        var product = Selected.ToResponse();

        product.Barcode = BarcodeInput.Trim();

        // Печатаем одну штуку: предпросмотр десяти одинаковых наклеек бесполезен
        var single = BuildOptions();
        single.Copies = 1;

        var lines = LabelRenderer.Render(product, single, _width);

        var text = new StringBuilder();

        foreach (var line in lines)
        {
            if (line.Barcode is { } barcode)
            {
                // Полосы на экране не рисуем — показываем место, которое они займут
                text.AppendLine(Center(new string('█', Math.Min(_width - 4, 24)), _width));
                text.AppendLine(Center(barcode.Data, _width));
                continue;
            }

            text.AppendLine(line.Align == PrintAlign.Center ? Center(line.Text, _width) : line.Text);
        }

        Preview = text.ToString().TrimEnd();
    }

    private static string Center(string text, int width)
        => text.Length >= width ? text : text.PadLeft((width + text.Length) / 2);

    // ===================== Действия =====================

    private async Task GenerateAsync()
    {
        if (Selected is null || _session.User is null)
        {
            ShowError(Loc.T("Labels_NoLogin"));
            return;
        }

        IsBusy = true;

        try
        {
            var code = await _labels.GenerateBarcodeAsync(
                Selected.ProductId, _session.User.UserId, CancellationToken.None);

            await LoadAsync();

            Reselect(Selected.ProductId);

            ShowInfo(Loc.F("Labels_Generated", code));
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

    private async Task SaveBarcodeAsync()
    {
        if (Selected is null || _session.User is null)
        {
            ShowError(Loc.T("Labels_NoLogin"));
            return;
        }

        if (!IsBarcodeValid)
        {
            ShowError(Loc.T("Labels_HintInvalid"));
            return;
        }

        IsBusy = true;

        try
        {
            var id = Selected.ProductId;

            await _labels.SetBarcodeAsync(id, BarcodeInput.Trim(), _session.User.UserId, CancellationToken.None);

            await LoadAsync();

            Reselect(id);

            ShowInfo(Loc.T("Labels_Saved"));
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

    private async Task PrintAsync()
    {
        if (Selected is null)
            return;

        // Печатаем то, что сохранено: иначе на наклейке окажется код,
        // которого нет в базе, и касса его не узнает
        if (!string.Equals(BarcodeInput.Trim(), Selected.Barcode, StringComparison.Ordinal))
        {
            ShowError(Loc.T("Labels_SaveFirst"));
            return;
        }

        IsBusy = true;

        try
        {
            await _labels.PrintLabelsAsync(Selected.ProductId, BuildOptions(), CancellationToken.None);

            ShowInfo(Loc.F("Labels_Printed", Copies));
        }
        catch (Exception ex)
        {
            ShowError(Loc.F("Labels_PrintFailed", ex.Message));
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void Reselect(long productId)
    {
        var again = Items.FirstOrDefault(i => i.ProductId == productId);

        if (again is not null)
            Select(again);
        else
            CloseEditor();
    }

    // ===================== Состояние =====================

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
                OnPropertyChanged(nameof(CanPrint));
        }
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

    // ===================== Загрузка =====================

    public async Task LoadAsync()
    {
        try
        {
            // Ширина ленты влияет на вёрстку этикетки — берём из настроек принтера
            _width = (await _printerSettings.GetAsync(CancellationToken.None)).CharsPerLine;

            var products = await _labels.GetProductsAsync(
                SearchText, OnlyWithoutBarcode, CancellationToken.None);

            Items.Clear();

            foreach (var product in products)
                Items.Add(new LabelProductItem(product));

            IsEmpty = Items.Count == 0;

            OnPropertyChanged(nameof(CountText));
            OnPropertyChanged(nameof(EmptyText));

            // Товар мог уйти из фильтра после выдачи кода
            if (Selected is not null && Items.All(i => i.ProductId != Selected.ProductId))
                CloseEditor();
        }
        catch (Exception ex)
        {
            ShowError(Loc.F("Labels_LoadFailed", ex.Message));
        }
    }

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
