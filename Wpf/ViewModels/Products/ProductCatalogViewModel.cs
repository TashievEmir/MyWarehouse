using System.Collections.ObjectModel;
using System.Windows.Input;
using Application.Contracts.Interfaces;
using Application.DTOs.Categories;
using Wpf.Common;
using Wpf.Services;

using Wpf.Localization;

namespace Wpf.ViewModels.Products;

/// <summary>
/// Каталог товаров: поиск по списку и панель карточки справа —
/// правка полей, списание количества и удаление.
/// </summary>
public class ProductCatalogViewModel : ViewModelBase
{
    private readonly IProductService _products;
    private readonly ICategoryService _categories;
    private readonly SessionService _session;

    public ObservableCollection<ProductListItem> Products { get; } = new();

    public ObservableCollection<CategoryResponse> Categories { get; } = new();

    public ICommand RefreshCommand { get; }
    public ICommand SelectCommand { get; }
    public ICommand ClearSearchCommand { get; }

    public ProductCatalogViewModel(IProductService products, ICategoryService categories, SessionService session)
    {
        _products = products;
        _categories = categories;
        _session = session;

        RefreshCommand = new AsyncRelayCommand(LoadAsync);
        SelectCommand = new RelayCommand<ProductListItem>(Select);
        ClearSearchCommand = new RelayCommand(() => SearchText = "");

        WatchLanguage();
    }

    private string _searchText = "";
    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                OnPropertyChanged(nameof(HasSearch));
                _ = LoadAsync(withCategories: false);
            }
        }
    }

    public bool HasSearch => SearchText.Length > 0;

    private ProductListItem? _selectedProduct;
    public ProductListItem? SelectedProduct
    {
        get => _selectedProduct;
        private set => SetProperty(ref _selectedProduct, value);
    }

    private ProductEditorViewModel? _editor;
    /// <summary>Панель карточки справа. null — панель закрыта.</summary>
    public ProductEditorViewModel? Editor
    {
        get => _editor;
        private set
        {
            if (SetProperty(ref _editor, value))
                OnPropertyChanged(nameof(HasEditor));
        }
    }

    public bool HasEditor => Editor is not null;

    private bool _isLoading;
    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
    }

    private bool _isEmpty;
    public bool IsEmpty
    {
        get => _isEmpty;
        private set => SetProperty(ref _isEmpty, value);
    }

    private string _errorMessage = "";
    public string ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (SetProperty(ref _errorMessage, value))
                OnPropertyChanged(nameof(HasError));
        }
    }

    public bool HasError => ErrorMessage.Length > 0;

    public string CountText => Products.Count == 0 ? "" : Loc.F("Catalog_Count", Products.Count);

    /// <summary>Перечитывает каталог и категории — вызывается при открытии вкладки.</summary>
    public Task LoadAsync() => LoadAsync(withCategories: true);

    /// <param name="withCategories">
    /// При наборе в поиске справочник не трогаем: перезаполнение общей коллекции
    /// сбрасывает выбор в открытой карточке.
    /// </param>
    private async Task LoadAsync(bool withCategories)
    {
        IsLoading = true;

        try
        {
            // Категорию могли завести на приёмке, пока каталог был открыт,
            // поэтому справочник перечитываем при каждом заходе, а не только пустой
            if (withCategories || Categories.Count == 0)
                await LoadCategoriesAsync();

            var catalog = await _products.GetCatalogAsync(SearchText, CancellationToken.None);

            var selectedId = SelectedProduct?.ProductId;

            Products.Clear();

            foreach (var product in catalog)
                Products.Add(new ProductListItem(product));

            IsEmpty = Products.Count == 0;
            ErrorMessage = "";

            // Панель могла остаться открытой на товаре, которого больше нет в списке
            if (selectedId is { } id)
            {
                var stillHere = Products.FirstOrDefault(p => p.ProductId == id);

                SelectedProduct = stillHere;

                if (stillHere is null)
                    Editor = null;
            }

            OnPropertyChanged(nameof(CountText));
        }
        catch (Exception ex)
        {
            ErrorMessage = Loc.F("Catalog_LoadFailed", ex.Message);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadCategoriesAsync()
    {
        var selectedId = Editor?.SelectedCategory?.Id;

        var categories = await _categories.GetAllAsync(CancellationToken.None);

        Categories.Clear();

        // Сортировка в памяти: SQLite не сравнивает кириллицу по культуре
        foreach (var category in categories.OrderBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase))
            Categories.Add(category);

        // Коллекция общая с открытой карточкой — очистка сбросила её выбор
        Editor?.RestoreCategory(selectedId);
    }

    private void Select(ProductListItem product)
    {
        SelectedProduct = product;

        Editor = new ProductEditorViewModel(
            product,
            Categories,
            _products,
            _session,
            LoadAsync,
            CloseEditor);
    }

    private void CloseEditor()
    {
        Editor = null;
        SelectedProduct = null;
    }

    /// <summary>Страница живёт синглтоном: после смены языка перечитываем её строки.</summary>
    private void WatchLanguage()
    {
        Loc.LanguageChanged += () =>
        {
            OnPropertyChanged(string.Empty);
            _ = LoadAsync();
        };
    }
}
