using Application.DTOs.Products;

using Wpf.Localization;

namespace Wpf.ViewModels.Products;

/// <summary>Строка каталога товаров.</summary>
public class ProductListItem
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

    public int InStock { get; }

    /// <summary>Есть продажи, поставки или списания — карточку удалять нельзя.</summary>
    public bool HasHistory { get; }

    public string CodeText => string.IsNullOrWhiteSpace(Barcode) ? SKU : Barcode!;

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
        InStock = product.InStock;
        HasHistory = product.HasHistory;
    }
}
