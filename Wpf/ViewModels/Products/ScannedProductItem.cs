using System.Windows.Input;
using Application.DTOs.Categories;
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

    private CategoryResponse? _category;
    public CategoryResponse? Category
    {
        get => _category;
        set
        {
            if (SetProperty(ref _category, value))
            {
                OnPropertyChanged(nameof(CategoryName));
                OnPropertyChanged(nameof(IsValid));
            }
        }
    }

    public string CategoryName => Category?.Name ?? "Без категории";

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
    public bool IsValid => !IsNew || (!string.IsNullOrWhiteSpace(Name) && Category is not null);

    public ICommand IncreaseCommand { get; }
    public ICommand DecreaseCommand { get; }

    private ScannedProductItem(long? productId, string barcode, string name, CategoryResponse? category, int inStockBefore, decimal cost)
    {
        ProductId = productId;
        Barcode = barcode;
        _name = name;
        _category = category;
        _cost = cost;
        InStockBefore = inStockBefore;

        IncreaseCommand = new RelayCommand(() => Quantity++);
        DecreaseCommand = new RelayCommand(() => Quantity--, () => Quantity > 1);
    }

    /// <summary>Товар найден в базе — правим только количество и цену.</summary>
    public static ScannedProductItem Known(ProductLookupResponse product, CategoryResponse? category)
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

    public ReceiveItemRequest ToRequest() => new()
    {
        ProductId = ProductId,
        Barcode = Barcode,
        Name = Name,
        CategoryId = Category?.Id,
        Quantity = Quantity,
        Cost = Cost
    };
}
