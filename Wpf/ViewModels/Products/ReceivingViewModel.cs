using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows.Input;
using Application.Contracts.Interfaces;
using Application.DTOs.Categories;
using Application.DTOs.Products;
using Wpf.Common;

namespace Wpf.ViewModels.Products;

/// <summary>
/// Приём товара по штрихкоду: сканирование партии и запись прихода на склад.
/// Остатки и статистика живут на странице «Статистика».
/// </summary>
/// <remarks>После удачного прихода поднимает <see cref="Received"/>,
/// чтобы каталог перечитал изменившиеся карточки и остатки.</remarks>
public class ReceivingViewModel : ViewModelBase
{
    private readonly IProductService _products;
    private readonly ICategoryService _categories;

    /// <summary>Категории для выбора у новых товаров, последний пункт — «создать новую».</summary>
    public ObservableCollection<CategoryOption> CategoryOptions { get; } = new();

    /// <summary>Отсканированные товары, ещё не записанные в базу.</summary>
    public ObservableCollection<ScannedProductItem> Scanned { get; } = new();

    public ICommand ScanCommand { get; }
    public ICommand RemoveScannedCommand { get; }
    public ICommand ClearScannedCommand { get; }
    public ICommand CreateCategoryCommand { get; }
    public ICommand SaveCommand { get; }

    /// <summary>Приход записан — каталог и остатки изменились.</summary>
    public event Action? Received;

    public ReceivingViewModel(IProductService products, ICategoryService categories)
    {
        _products = products;
        _categories = categories;

        ScanCommand = new AsyncRelayCommand(ScanAsync);
        RemoveScannedCommand = new RelayCommand<ScannedProductItem>(RemoveScanned);
        ClearScannedCommand = new RelayCommand(ClearScanned);
        CreateCategoryCommand = new AsyncRelayCommand<ScannedProductItem>(CreateCategoryAsync);
        SaveCommand = new AsyncRelayCommand(SaveAsync);

        Scanned.CollectionChanged += OnScannedChanged;

        _ = LoadCategoriesAsync();
    }

    // ===================== Состояние =====================

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

    // ===================== Категории =====================

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
                ShowInfo($"Новый штрихкод {barcode} — укажите название, категорию и цену продажи");
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
            ShowError($"Штрихкод {invalid.Barcode}: {invalid.ValidationHint}");
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

            Received?.Invoke();

            ShowInfo($"Добавлено {positions} поз. · {quantity} шт. — остатки видны в «Статистике»");
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
