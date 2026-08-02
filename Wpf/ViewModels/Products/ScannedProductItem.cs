using System.Windows.Input;
using Application.DTOs.Products;
using Wpf.Common;

using Wpf.Localization;

namespace Wpf.ViewModels.Products;

/// <summary>
/// Строка списка сканирования. Для известного штрихкода название и категория
/// подставляются из базы, для нового — их заполняет пользователь.
/// </summary>
public class ScannedProductItem : ViewModelBase
{
    public long? ProductId { get; }
    public string Barcode { get; }

    /// <summary>true — такого штрихкода в базе нет, товар будет заведён при сохранении.</summary>
    public bool IsNew => ProductId is null;

    /// <summary>Остаток товара на складе до этого прихода.</summary>
    public int InStockBefore { get; }

    private string _name;
    public string Name
    {
        get => _name;
        set
        {
            if (SetProperty(ref _name, value))
                OnPropertyChanged(nameof(IsValid));
        }
    }

    private CategoryOption? _selectedCategory;
    public CategoryOption? SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            // «+ Новая категория…» — не выбор, а переход к вводу названия
            if (value is { IsCreateNew: true })
            {
                NewCategoryName = "";
                IsCreatingCategory = true;

                // возвращаем комбобоксу прежнее значение
                OnPropertyChanged();
                return;
            }

            if (!SetProperty(ref _selectedCategory, value))
                return;

            OnPropertyChanged(nameof(CategoryName));
            OnPropertyChanged(nameof(IsValid));
        }
    }

    public string CategoryName => SelectedCategory?.Name ?? Loc.T("Scanned_NoCategory");

    public string InStockBeforeText => Loc.F("Scanned_InStockBefore", InStockBefore);

    private bool _isCreatingCategory;
    /// <summary>В строке вместо списка категорий показывается поле ввода новой.</summary>
    public bool IsCreatingCategory
    {
        get => _isCreatingCategory;
        set => SetProperty(ref _isCreatingCategory, value);
    }

    private string _newCategoryName = "";
    public string NewCategoryName
    {
        get => _newCategoryName;
        set => SetProperty(ref _newCategoryName, value);
    }

    private int _quantity = 1;
    public int Quantity
    {
        get => _quantity;
        set
        {
            if (SetProperty(ref _quantity, value < 1 ? 1 : value))
                OnPropertyChanged(nameof(TotalCost));
        }
    }

    private decimal _cost;
    /// <summary>Цена закупки за штуку.</summary>
    public decimal Cost
    {
        get => _cost;
        set
        {
            if (SetProperty(ref _cost, value < 0 ? 0 : value))
                OnPropertyChanged(nameof(TotalCost));
        }
    }

    private decimal _price;
    /// <summary>Цена продажи: уходит в карточку товара и подставляется на кассе.</summary>
    public decimal Price
    {
        get => _price;
        set
        {
            if (SetProperty(ref _price, value < 0 ? 0 : value))
                OnPropertyChanged(nameof(IsValid));
        }
    }

    public decimal TotalCost => Quantity * Cost;

    /// <summary>
    /// Новый товар нельзя сохранить без названия, категории и цены продажи —
    /// иначе его не пробить на кассе.
    /// </summary>
    public bool IsValid => !IsNew ||
                           (!string.IsNullOrWhiteSpace(Name) && SelectedCategory?.Category is not null && Price > 0);

    /// <summary>Чего именно не хватает в строке — для понятного сообщения.</summary>
    public string? ValidationHint
    {
        get
        {
            if (!IsNew) return null;
            if (string.IsNullOrWhiteSpace(Name)) return Loc.T("Scanned_NeedName");
            if (SelectedCategory?.Category is null) return Loc.T("Scanned_NeedCategory");
            if (Price <= 0) return Loc.T("Scanned_NeedPrice");

            return null;
        }
    }

    public ICommand IncreaseCommand { get; }
    public ICommand DecreaseCommand { get; }
    public ICommand CancelNewCategoryCommand { get; }

    private ScannedProductItem(long? productId, string barcode, string name, CategoryOption? category, int inStockBefore, decimal cost, decimal price)
    {
        ProductId = productId;
        Barcode = barcode;
        _name = name;
        _selectedCategory = category;
        _cost = cost;
        _price = price;
        InStockBefore = inStockBefore;

        IncreaseCommand = new RelayCommand(() => Quantity++);
        DecreaseCommand = new RelayCommand(() => Quantity--, () => Quantity > 1);
        CancelNewCategoryCommand = new RelayCommand(() => IsCreatingCategory = false);
    }

    /// <summary>
    /// Товар найден в базе: подставляем его цены, их можно обновить этой поставкой.
    /// </summary>
    public static ScannedProductItem Known(ProductLookupResponse product, CategoryOption? category)
        => new(
            product.ProductId,
            product.Barcode ?? "",
            product.Name,
            category,
            product.InStock,
            product.CostPerUnit ?? 0m,
            product.PricePerUnit);

    /// <summary>Штрихкод неизвестен — пользователь вводит название, категорию и цены.</summary>
    public static ScannedProductItem New(string barcode)
        => new(null, barcode, "", null, 0, 0m, 0m);

    /// <summary>Подставляет категорию, созданную из этой же строки.</summary>
    public void ApplyCategory(CategoryOption category)
    {
        IsCreatingCategory = false;
        NewCategoryName = "";

        SelectedCategory = category;
    }

    public ReceiveItemRequest ToRequest() => new()
    {
        ProductId = ProductId,
        Barcode = Barcode,
        Name = Name,
        CategoryId = SelectedCategory?.Category?.Id,
        Quantity = Quantity,
        Cost = Cost,
        Price = Price
    };
}
