using System.Windows.Input;
using Application.DTOs.Products;
using Wpf.Common;

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

    public string CategoryName => SelectedCategory?.Name ?? "Без категории";

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
    public decimal Cost
    {
        get => _cost;
        set
        {
            if (SetProperty(ref _cost, value < 0 ? 0 : value))
                OnPropertyChanged(nameof(TotalCost));
        }
    }

    public decimal TotalCost => Quantity * Cost;

    /// <summary>Новый товар нельзя сохранить без названия и категории.</summary>
    public bool IsValid => !IsNew || (!string.IsNullOrWhiteSpace(Name) && SelectedCategory?.Category is not null);

    public ICommand IncreaseCommand { get; }
    public ICommand DecreaseCommand { get; }
    public ICommand CancelNewCategoryCommand { get; }

    private ScannedProductItem(long? productId, string barcode, string name, CategoryOption? category, int inStockBefore, decimal cost)
    {
        ProductId = productId;
        Barcode = barcode;
        _name = name;
        _selectedCategory = category;
        _cost = cost;
        InStockBefore = inStockBefore;

        IncreaseCommand = new RelayCommand(() => Quantity++);
        DecreaseCommand = new RelayCommand(() => Quantity--, () => Quantity > 1);
        CancelNewCategoryCommand = new RelayCommand(() => IsCreatingCategory = false);
    }

    /// <summary>Товар найден в базе — правим только количество и цену.</summary>
    public static ScannedProductItem Known(ProductLookupResponse product, CategoryOption? category)
        => new(
            product.ProductId,
            product.Barcode ?? "",
            product.Name,
            category,
            product.InStock,
            product.CostPerUnit ?? 0m);

    /// <summary>Штрихкод неизвестен — пользователь вводит название и категорию.</summary>
    public static ScannedProductItem New(string barcode)
        => new(null, barcode, "", null, 0, 0m);

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
        Cost = Cost
    };
}
