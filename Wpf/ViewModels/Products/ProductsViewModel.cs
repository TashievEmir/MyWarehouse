using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows.Input;
using Application.Contracts.Interfaces;
using Application.DTOs.Categories;
using Application.DTOs.Products;
using Wpf.Common;

namespace Wpf.ViewModels.Products;

public class ProductsViewModel : ViewModelBase
{
    private readonly IProductService _products;
    private readonly ICategoryService _categories;

    /// <summary>Статистика: категории с товарами внутри.</summary>
    public ObservableCollection<CategoryStatItem> Stats { get; } = new();

    /// <summary>Категории для выбора у новых товаров, последний пункт — «создать новую».</summary>
    public ObservableCollection<CategoryOption> CategoryOptions { get; } = new();

    /// <summary>Отсканированные товары, ещё не записанные в базу.</summary>
    public ObservableCollection<ScannedProductItem> Scanned { get; } = new();

    public ICommand RefreshCommand { get; }
    public ICommand ResetPeriodCommand { get; }
    public ICommand ScanCommand { get; }
    public ICommand RemoveScannedCommand { get; }
    public ICommand ClearScannedCommand { get; }
    public ICommand CreateCategoryCommand { get; }
    public ICommand SaveCommand { get; }

    public ProductsViewModel(IProductService products, ICategoryService categories)
    {
        _products = products;
        _categories = categories;

        RefreshCommand = new AsyncRelayCommand(LoadStatsAsync);
        ResetPeriodCommand = new RelayCommand(ResetPeriod);
        ScanCommand = new AsyncRelayCommand(ScanAsync);
        RemoveScannedCommand = new RelayCommand<ScannedProductItem>(RemoveScanned);
        ClearScannedCommand = new RelayCommand(ClearScanned);
        CreateCategoryCommand = new AsyncRelayCommand<ScannedProductItem>(CreateCategoryAsync);
        SaveCommand = new AsyncRelayCommand(SaveAsync);

        Scanned.CollectionChanged += OnScannedChanged;

        _ = InitializeAsync();
    }

    // ===================== Фильтр по датам =====================

    private DateTime? _from;
    public DateTime? From
    {
        get => _from;
        set
        {
            if (SetProperty(ref _from, value))
                OnPropertyChanged(nameof(PeriodText));
        }
    }

    private DateTime? _to;
    public DateTime? To
    {
        get => _to;
        set
        {
            if (SetProperty(ref _to, value))
                OnPropertyChanged(nameof(PeriodText));
        }
    }

    public string PeriodText
    {
        get
        {
            if (From is null && To is null) return "за всё время";
            if (From is not null && To is null) return $"с {From:dd.MM.yyyy}";
            if (From is null && To is not null) return $"по {To:dd.MM.yyyy}";

            return $"{From:dd.MM.yyyy} — {To:dd.MM.yyyy}";
        }
    }

    // ===================== Состояние =====================

    private bool _isLoading;
    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
    }

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
                OnPropertyChanged(nameof(CanSave));
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

    private int _totalInStock;
    public int TotalInStock
    {
        get => _totalInStock;
        private set => SetProperty(ref _totalInStock, value);
    }

    private int _totalReceived;
    public int TotalReceived
    {
        get => _totalReceived;
        private set => SetProperty(ref _totalReceived, value);
    }

    private bool _isEmpty;
    public bool IsEmpty
    {
        get => _isEmpty;
        private set => SetProperty(ref _isEmpty, value);
    }

    // ===================== Панель сканирования =====================

    private string _barcodeInput = "";
    public string BarcodeInput
    {
        get => _barcodeInput;
        set => SetProperty(ref _barcodeInput, value);
    }

    private string _supplierName = "";
    public string SupplierName
    {
        get => _supplierName;
        set => SetProperty(ref _supplierName, value);
    }

    public bool HasScanned => Scanned.Count > 0;

    public int ScannedQuantity => Scanned.Sum(x => x.Quantity);

    public string ScannedSummary => Scanned.Count == 0
        ? "Список пуст — отсканируйте штрихкоды"
        : $"{Scanned.Count} поз. · {ScannedQuantity} шт.";

    public bool CanSave => Scanned.Count > 0 && !IsBusy;

    // ===================== Загрузка =====================

    private async Task InitializeAsync()
    {
        await LoadCategoriesAsync();
        await LoadStatsAsync();
    }

    private async Task LoadCategoriesAsync()
    {
        try
        {
            var categories = await _categories.GetAllAsync(CancellationToken.None);

            CategoryOptions.Clear();

            foreach (var category in categories.OrderBy(c => c.Name))
                CategoryOptions.Add(CategoryOption.For(category));

            CategoryOptions.Add(CategoryOption.CreateNew());
        }
        catch (Exception ex)
        {
            ShowError($"Не удалось загрузить категории: {ex.Message}");
        }
    }

    /// <summary>
    /// Заводит категорию прямо из строки сканирования — для товара,
    /// который не подходит ни под одну из существующих.
    /// </summary>
    private async Task CreateCategoryAsync(ScannedProductItem item)
    {
        var name = item.NewCategoryName.Trim();

        if (name.Length == 0)
        {
            ShowError("Введите название категории");
            return;
        }

        var existing = CategoryOptions.FirstOrDefault(o =>
            o.Category is not null &&
            string.Equals(o.Category.Name, name, StringComparison.CurrentCultureIgnoreCase));

        if (existing is not null)
        {
            item.ApplyCategory(existing);
            ShowInfo($"Категория «{existing.Category!.Name}» уже есть — выбрана она");
            return;
        }

        try
        {
            var id = await _categories.CreateAsync(new CreateCategoryRequest { Name = name }, CancellationToken.None);
            var created = await _categories.GetAsync(id, CancellationToken.None);

            if (created is null)
            {
                ShowError("Не удалось создать категорию");
                return;
            }

            var option = CategoryOption.For(created);

            InsertCategoryOption(option);
            item.ApplyCategory(option);

            ShowInfo($"Категория «{created.Name}» добавлена");
        }
        catch (Exception ex)
        {
            ShowError($"Не удалось создать категорию: {ex.Message}");
        }
    }

    /// <summary>Вставляет категорию по алфавиту, но перед пунктом «создать новую».</summary>
    private void InsertCategoryOption(CategoryOption option)
    {
        var index = 0;

        while (index < CategoryOptions.Count &&
               CategoryOptions[index].Category is { } current &&
               string.Compare(current.Name, option.Category!.Name, StringComparison.CurrentCulture) < 0)
        {
            index++;
        }

        CategoryOptions.Insert(index, option);
    }

    private async Task LoadStatsAsync()
    {
        IsLoading = true;

        try
        {
            var from = From is null ? (DateTimeOffset?)null : new DateTimeOffset(From.Value.Date);

            // Дата «по» включительно — берём начало следующего дня как верхнюю границу
            var toExclusive = To is null ? (DateTimeOffset?)null : new DateTimeOffset(To.Value.Date.AddDays(1));

            var categories = await _products.GetStockByCategoryAsync(from, toExclusive, CancellationToken.None);

            Stats.Clear();

            foreach (var category in categories)
                Stats.Add(new CategoryStatItem(category));

            TotalInStock = categories.Sum(c => c.InStock);
            TotalReceived = categories.Sum(c => c.Received);
            IsEmpty = Stats.Count == 0;
        }
        catch (Exception ex)
        {
            ShowError($"Не удалось загрузить статистику: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void ResetPeriod()
    {
        From = null;
        To = null;

        _ = LoadStatsAsync();
    }

    // ===================== Сканирование =====================

    private async Task ScanAsync()
    {
        var barcode = BarcodeInput.Trim();

        BarcodeInput = "";

        if (barcode.Length == 0)
            return;

        // Тот же штрихкод второй раз — просто увеличиваем количество
        var existing = Scanned.FirstOrDefault(x => x.Barcode == barcode);

        if (existing is not null)
        {
            existing.Quantity++;
            RefreshScannedSummary();
            ShowInfo($"{DisplayName(existing)} — {existing.Quantity} шт.");
            return;
        }

        try
        {
            var found = await _products.FindByBarcodeAsync(barcode, CancellationToken.None);

            var item = found is null
                ? ScannedProductItem.New(barcode)
                : ScannedProductItem.Known(found, FindCategory(found.CategoryId));

            item.PropertyChanged += OnScannedItemChanged;

            Scanned.Insert(0, item);

            if (found is null)
                ShowInfo($"Новый штрихкод {barcode} — укажите название и категорию");
            else
                ShowInfo($"{found.Name} · {found.CategoryName}");
        }
        catch (Exception ex)
        {
            ShowError($"Ошибка при сканировании: {ex.Message}");
        }
    }

    private void RemoveScanned(ScannedProductItem item)
    {
        item.PropertyChanged -= OnScannedItemChanged;

        Scanned.Remove(item);
    }

    private void ClearScanned()
    {
        foreach (var item in Scanned)
            item.PropertyChanged -= OnScannedItemChanged;

        Scanned.Clear();
        StatusMessage = "";
    }

    private async Task SaveAsync()
    {
        if (Scanned.Count == 0)
        {
            ShowError("Список пуст — сначала отсканируйте товары");
            return;
        }

        var invalid = Scanned.FirstOrDefault(x => !x.IsValid);

        if (invalid is not null)
        {
            ShowError($"У товара {invalid.Barcode} не заполнены название или категория");
            return;
        }

        IsBusy = true;

        try
        {
            var request = new ReceiveProductsRequest
            {
                SupplierName = SupplierName,
                Items = Scanned.Select(x => x.ToRequest()).ToList()
            };

            var positions = Scanned.Count;
            var quantity = ScannedQuantity;

            await _products.ReceiveAsync(request, CancellationToken.None);

            ClearScanned();

            await LoadStatsAsync();

            ShowInfo($"Добавлено {positions} поз. · {quantity} шт.");
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

    // ===================== Вспомогательное =====================

    private CategoryOption? FindCategory(long categoryId)
        => CategoryOptions.FirstOrDefault(o => o.Category?.Id == categoryId);

    private static string DisplayName(ScannedProductItem item)
        => string.IsNullOrWhiteSpace(item.Name) ? item.Barcode : item.Name;

    private void OnScannedChanged(object? sender, NotifyCollectionChangedEventArgs e)
        => RefreshScannedSummary();

    private void OnScannedItemChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ScannedProductItem.Quantity))
            RefreshScannedSummary();
    }

    private void RefreshScannedSummary()
    {
        OnPropertyChanged(nameof(HasScanned));
        OnPropertyChanged(nameof(ScannedQuantity));
        OnPropertyChanged(nameof(ScannedSummary));
        OnPropertyChanged(nameof(CanSave));
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
