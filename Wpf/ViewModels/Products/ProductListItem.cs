using Application.DTOs.Products;
using Wpf.Common;

using Wpf.Localization;

namespace Wpf.ViewModels.Products;

/// <summary>Строка каталога товаров.</summary>
public class ProductListItem : ViewModelBase
{
    public long ProductId { get; }
    public string Name { get; }
    public string SKU { get; }
    public string? Barcode { get; }
    public string? Description { get; }

    public long CategoryId { get; }
    public string CategoryName { get; }

    public decimal PricePerUnit { get; }
    public decimal? CostPerUnit { get; }

    /// <summary>Есть продажи, поставки или списания — карточку удалять нельзя.</summary>
    public bool HasHistory { get; }

    public string CodeText => string.IsNullOrWhiteSpace(Barcode) ? SKU : Barcode!;

    private int _inStock;
    public int InStock
    {
        get => _inStock;
        private set
        {
            if (!SetProperty(ref _inStock, value))
                return;

            OnPropertyChanged(nameof(StockText));
            OnPropertyChanged(nameof(IsOutOfStock));
        }
    }

    public string StockText => Loc.F("Product_StockPcs", InStock);

    public bool IsOutOfStock => InStock <= 0;

    public ProductListItem(ProductListItemResponse product)
    {
        ProductId = product.ProductId;
        Name = product.Name;
        SKU = product.SKU;
        Barcode = product.Barcode;
        Description = product.Description;
        CategoryId = product.CategoryId;
        CategoryName = product.CategoryName;
        PricePerUnit = product.PricePerUnit;
        CostPerUnit = product.CostPerUnit;
        HasHistory = product.HasHistory;

        _inStock = product.InStock;
    }

    /// <summary>Остаток поправили в карточке — строка списка должна это показать.</summary>
    public void ApplyStock(int quantity) => InStock = quantity;
}
