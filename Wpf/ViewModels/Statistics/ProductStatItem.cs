using Application.DTOs.Products;

namespace Wpf.ViewModels.Statistics;

/// <summary>Строка товара в статистике: «есть / поступило за период».</summary>
public class ProductStatItem
{
    public long ProductId { get; }
    public string Name { get; }
    public string SKU { get; }
    public string? Barcode { get; }

    public int InStock { get; }
    public int Received { get; }

    /// <summary>«12 / 30» — сколько осталось из того, что приходило за период.</summary>
    public string StockRatio => $"{InStock} / {Received}";

    public string BarcodeText => string.IsNullOrWhiteSpace(Barcode) ? SKU : Barcode;

    public ProductStatItem(ProductStockResponse product)
    {
        ProductId = product.ProductId;
        Name = product.Name;
        SKU = product.SKU;
        Barcode = product.Barcode;
        InStock = product.InStock;
        Received = product.Received;
    }
}
