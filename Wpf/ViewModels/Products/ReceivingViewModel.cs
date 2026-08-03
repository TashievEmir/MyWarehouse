using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows.Input;
using Application.Contracts.Interfaces;
using Application.DTOs.Categories;
using Application.DTOs.Products;
using Wpf.Common;
using Wpf.Services;

using Wpf.Localization;

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
    private readonly ISupplierService _suppliers;
    private readonly SessionService _session;

    /// <summary>Категории для выбора у новых товаров, последний пункт — «создать новую».</summary>
    public ObservableCollection<CategoryOption> CategoryOptions { get; } = new();

    /// <summary>Поставщики из справочника, последний пункт — «создать нового».</summary>
    public ObservableCollection<SupplierOption> SupplierOptions { get; } = new();

    /// <summary>Отсканированные товары, ещё не записанные в базу.</summary>
    public ObservableCollection<ScannedProductItem> Scanned { get; } = new();

    public ICommand ScanCommand { get; }
    public ICommand RemoveScannedCommand { get; }
    public ICommand ClearScannedCommand { get; }
    public ICommand CreateCategoryCommand { get; }
    public ICommand CreateSupplierCommand { get; }
    public ICommand CancelNewSupplierCommand { get; }
    public ICommand SaveCommand { get; }

    /// <summary>Приход записан — каталог и остатки изменились.</summary>
    public event Action? Received;

    public ReceivingViewModel(
        IProductService products,
        ICategoryService categories,
        ISupplierService suppliers,
        SessionService session)
    {
        _products = products;
        _categories = categories;
        _suppliers = suppliers;
        _session = session;

        ScanCommand = new AsyncRelayCommand(ScanAsync);
        RemoveScannedCommand = new RelayCommand<ScannedProductItem>(RemoveScanned);
        ClearScannedCommand = new RelayCommand(ClearScanned);
        CreateCategoryCommand = new AsyncRelayCommand<ScannedProductItem>(CreateCategoryAsync);
        CreateSupplierCommand = new AsyncRelayCommand(CreateSupplierAsync);
        CancelNewSupplierCommand = new RelayCommand(CancelNewSupplier);
        SaveCommand = new AsyncRelayCommand(SaveAsync);

        Scanned.CollectionChanged += OnScannedChanged;

        _ = LoadCategoriesAsync();
        _ = LoadSuppliersAsync();

        WatchLanguage();
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

    // ===================== Поставщик =====================

    private SupplierOption? _selectedSupplier;
    /// <summary>Выбор «создать нового» разворачивает поле ввода вместо списка.</summary>
    public SupplierOption? SelectedSupplier
    {
        get => _selectedSupplier;
        set
        {
            if (!SetProperty(ref _selectedSupplier, value))
                return;

            if (value?.IsCreateNew == true)
            {
                IsCreatingSupplier = true;
                NewSupplierName = "";
                _selectedSupplier = null;

                OnPropertyChanged(nameof(SelectedSupplier));
            }
        }
    }

    private bool _isCreatingSupplier;
    public bool IsCreatingSupplier
    {
        get => _isCreatingSupplier;
        private set => SetProperty(ref _isCreatingSupplier, value);
    }

    private string _newSupplierName = "";
    public string NewSupplierName
    {
        get => _newSupplierName;
        set => SetProperty(ref _newSupplierName, value);
    }

    public bool HasScanned => Scanned.Count > 0;

    public int ScannedQuantity => Scanned.Sum(x => x.Quantity);

    public string ScannedSummary => Scanned.Count == 0
        ? Loc.T("Receiving_EmptySummary")
        : Loc.F("Receiving_Summary", Scanned.Count, ScannedQuantity);

    public bool CanSave => Scanned.Count > 0 && !IsBusy;

    // ===================== Категории =====================

    /// <summary>Приёмка живёт синглтоном: после смены языка обновляем подписи и список категорий.</summary>
    private void WatchLanguage()
    {
        Loc.LanguageChanged += () =>
        {
            OnPropertyChanged(string.Empty);
            _ = LoadCategoriesAsync();
            _ = LoadSuppliersAsync();
        };
    }

    /// <summary>Справочник поставщиков: наполняется сам при каждом приходе.</summary>
    private async Task LoadSuppliersAsync()
    {
        try
        {
            var picked = SelectedSupplier?.Supplier?.Id;

            var suppliers = await _suppliers.GetAllAsync(CancellationToken.None);

            SupplierOptions.Clear();

            foreach (var supplier in suppliers)
                SupplierOptions.Add(SupplierOption.For(supplier));

            SupplierOptions.Add(SupplierOption.CreateNew());

            // Выбор не сбрасываем: после сохранения прихода часто везут ещё одну партию
            if (picked is not null)
                SelectedSupplier = SupplierOptions.FirstOrDefault(o => o.Supplier?.Id == picked);
        }
        catch (Exception ex)
        {
            ShowError(Loc.F("Supplier_LoadFailed", ex.Message));
        }
    }

    /// <summary>Заводит поставщика прямо на приёмке — как категорию из строки товара.</summary>
    private async Task CreateSupplierAsync()
    {
        var name = NewSupplierName.Trim();

        if (name.Length == 0)
        {
            ShowError(Loc.T("Err_NeedSupplierName"));
            return;
        }

        var existing = SupplierOptions.FirstOrDefault(o =>
            o.Supplier is not null &&
            string.Equals(o.Supplier.Name, name, StringComparison.CurrentCultureIgnoreCase));

        if (existing is not null)
        {
            ApplySupplier(existing);
            ShowInfo(Loc.F("Supplier_Exists", existing.Supplier!.Name));
            return;
        }

        try
        {
            var created = await _suppliers.EnsureAsync(name, CancellationToken.None);

            var option = SupplierOption.For(created);

            InsertSupplierOption(option);
            ApplySupplier(option);

            ShowInfo(Loc.F("Supplier_Added", created.Name));
        }
        catch (Exception ex)
        {
            ShowError(Loc.F("Supplier_CreateError", ex.Message));
        }
    }

    private void CancelNewSupplier()
    {
        IsCreatingSupplier = false;
        NewSupplierName = "";
    }

    private void ApplySupplier(SupplierOption option)
    {
        IsCreatingSupplier = false;
        NewSupplierName = "";

        SelectedSupplier = option;
    }

    /// <summary>Вставляет поставщика по алфавиту, но перед пунктом «создать нового».</summary>
    private void InsertSupplierOption(SupplierOption option)
    {
        var index = 0;

        while (index < SupplierOptions.Count &&
               SupplierOptions[index].Supplier is { } current &&
               string.Compare(current.Name, option.Supplier!.Name, StringComparison.CurrentCulture) < 0)
        {
            index++;
        }

        SupplierOptions.Insert(index, option);
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
            ShowError(Loc.F("Receiving_CategoriesFailed", ex.Message));
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
            ShowError(Loc.T("Receiving_NeedCategoryName"));
            return;
        }

        var existing = CategoryOptions.FirstOrDefault(o =>
            o.Category is not null &&
            string.Equals(o.Category.Name, name, StringComparison.CurrentCultureIgnoreCase));

        if (existing is not null)
        {
            item.ApplyCategory(existing);
            ShowInfo(Loc.F("Receiving_CategoryExists", existing.Category!.Name));
            return;
        }

        try
        {
            var id = await _categories.CreateAsync(new CreateCategoryRequest { Name = name }, CancellationToken.None);
            var created = await _categories.GetAsync(id, CancellationToken.None);

            if (created is null)
            {
                ShowError(Loc.T("Receiving_CategoryCreateFailed"));
                return;
            }

            var option = CategoryOption.For(created);

            InsertCategoryOption(option);
            item.ApplyCategory(option);

            ShowInfo(Loc.F("Receiving_CategoryAdded", created.Name));
        }
        catch (Exception ex)
        {
            ShowError(Loc.F("Receiving_CategoryCreateError", ex.Message));
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
            ShowInfo(Loc.F("Receiving_ScannedExisting", DisplayName(existing), existing.Quantity));
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
                ShowInfo(Loc.F("Receiving_ScannedNew", barcode));
            else
                ShowInfo(Loc.F("Receiving_ScannedFound", found.Name, found.CategoryName));
        }
        catch (Exception ex)
        {
            ShowError(Loc.F("Receiving_ScanError", ex.Message));
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
            ShowError(Loc.T("Receiving_ListEmpty"));
            return;
        }

        var invalid = Scanned.FirstOrDefault(x => !x.IsValid);

        if (invalid is not null)
        {
            ShowError(Loc.F("Receiving_Invalid", invalid.Barcode, invalid.ValidationHint));
            return;
        }

        IsBusy = true;

        try
        {
            // Имя, набранное в поле «создать нового», сохранится в справочнике само
            var request = new ReceiveProductsRequest
            {
                SupplierId = SelectedSupplier?.Supplier?.Id,
                SupplierName = IsCreatingSupplier ? NewSupplierName : SelectedSupplier?.Supplier?.Name,
                UserId = _session.User?.UserId ?? 0,
                Items = Scanned.Select(x => x.ToRequest()).ToList()
            };

            var positions = Scanned.Count;
            var quantity = ScannedQuantity;

            await _products.ReceiveAsync(request, CancellationToken.None);

            ClearScanned();

            // Новый поставщик мог появиться при сохранении — перечитываем список
            // и подставляем его, чтобы следующая партия уже была привязана
            var typed = IsCreatingSupplier ? NewSupplierName.Trim() : null;

            await LoadSuppliersAsync();

            if (typed is { Length: > 0 })
            {
                var option = SupplierOptions.FirstOrDefault(o =>
                    o.Supplier is not null &&
                    string.Equals(o.Supplier.Name, typed, StringComparison.CurrentCultureIgnoreCase));

                if (option is not null)
                    ApplySupplier(option);
            }

            Received?.Invoke();

            ShowInfo(Loc.F("Receiving_Saved", positions, quantity));
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
