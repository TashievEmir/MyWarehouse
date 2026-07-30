using System.Windows.Input;
using Application.DTOs.Products;
using Application.DTOs.Sales;
using Wpf.Common;

namespace Wpf.ViewModels.Sales;

/// <summary>Позиция чека: товар, цена продажи и количество.</summary>
public class SaleLineItem : ViewModelBase
{
    public long ProductId { get; }
    public string Name { get; }
    public string Barcode { get; }

    /// <summary>Остаток на складе на момент последнего сканирования этого товара.</summary>
    public int InStock { get; private set; }

    private decimal _price;
    /// <summary>Цена берётся из карточки товара, но кассир может её поправить.</summary>
    public decimal Price
    {
        get => _price;
        set
        {
            if (SetProperty(ref _price, value < 0 ? 0 : value))
                RaiseTotals();
        }
    }

    private int _quantity = 1;
    public int Quantity
    {
        get => _quantity;
        set
        {
            if (SetProperty(ref _quantity, value < 1 ? 1 : value))
                RaiseTotals();
        }
    }

    public decimal Sum => Price * Quantity;

    /// <summary>Товар заведён приёмкой без цены — продать его так нельзя.</summary>
    public bool HasNoPrice => Price <= 0;

    /// <summary>В чеке больше, чем лежит на складе.</summary>
    public bool ExceedsStock => Quantity > InStock;

    public string StockText => $"на складе: {InStock}";

    public ICommand IncreaseCommand { get; }
    public ICommand DecreaseCommand { get; }

    public SaleLineItem(ProductLookupResponse product)
    {
        ProductId = product.ProductId;
        Name = product.Name;
        Barcode = product.Barcode ?? "";
        InStock = product.InStock;
        _price = product.PricePerUnit;

        IncreaseCommand = new RelayCommand(() => Quantity++);
        DecreaseCommand = new RelayCommand(() => Quantity--, () => Quantity > 1);
    }

    /// <summary>
    /// Остаток мог измениться после того, как строка попала в чек — например,
    /// товар приняли на складе. Обновляем при повторном сканировании.
    /// </summary>
    public void UpdateStock(int inStock)
    {
        if (InStock == inStock)
            return;

        InStock = inStock;

        OnPropertyChanged(nameof(InStock));
        OnPropertyChanged(nameof(StockText));
        OnPropertyChanged(nameof(ExceedsStock));
    }

    public SaleLineRequest ToRequest() => new()
    {
        ProductId = ProductId,
        Quantity = Quantity,
        Price = Price
    };

    private void RaiseTotals()
    {
        OnPropertyChanged(nameof(Sum));
        OnPropertyChanged(nameof(HasNoPrice));
        OnPropertyChanged(nameof(ExceedsStock));
    }
}
