using System.Globalization;
using Application.DTOs.Dashboard;
using Domain.Enums;

using Wpf.Localization;

namespace Wpf.ViewModels.Dashboard;

/// <summary>Столбик графика выручки.</summary>
public class RevenueBarItem
{
    private static CultureInfo Ui => Wpf.Localization.Loc.Instance.Culture;

    /// <summary>Высота столбика в пикселях: считаем в модели, чтобы XAML остался без конвертеров.</summary>
    public const double MaxBarHeight = 120;

    public DateTime Date { get; }
    public decimal Amount { get; }
    public int Receipts { get; }

    public double BarHeight { get; }

    public bool IsToday => Date == DateTime.Today;

    public string DayLabel => Date.ToString("dd.MM", Ui);

    public string AmountLabel => Amount == 0 ? "" : Amount.ToString("N0", Ui);

    public string Tooltip => Loc.F("Dash_BarTooltip", Date.ToString("d MMMM", Ui), Amount, Receipts);

    public RevenueBarItem(DailyRevenueResponse day, decimal maxAmount)
    {
        Date = day.Date;
        Amount = day.Amount;
        Receipts = day.Receipts;

        // Пустой день оставляем видимой полоской, иначе график выглядит рваным
        BarHeight = maxAmount <= 0
            ? 2
            : Math.Max(2, (double)(day.Amount / maxAmount) * MaxBarHeight);
    }
}

/// <summary>Способ оплаты в разбивке за день.</summary>
public class PaymentSliceItem
{
    public string Name { get; }
    public decimal Amount { get; }
    public int Receipts { get; }
    public double SharePercent { get; }

    public string ReceiptsLabel => Loc.F("Dash_ReceiptsLabel", Receipts);

    public PaymentSliceItem(PaymentSliceResponse slice, decimal total)
    {
        Name = slice.Method switch
        {
            PaymentMethod.Cash     => Loc.T("Payment_Cash"),
            PaymentMethod.Card     => Loc.T("Payment_Card"),
            PaymentMethod.Transfer => Loc.T("Payment_Transfer"),
            PaymentMethod.Credit   => Loc.T("Payment_Credit"),
            _                      => Loc.T("Payment_Unknown"),
        };

        Amount = slice.Amount;
        Receipts = slice.Receipts;
        SharePercent = total <= 0 ? 0 : (double)(slice.Amount / total) * 100;
    }
}

/// <summary>Товар, который заканчивается.</summary>
public class LowStockItem
{
    public string Name { get; }
    public string CategoryName { get; }
    public string? Barcode { get; }
    public int InStock { get; }

    public bool IsOut => InStock <= 0;

    public string StockLabel => IsOut ? Loc.T("Dash_StockOut") : Loc.F("Product_StockPcs", InStock);

    public LowStockItem(LowStockResponse product)
    {
        Name = product.Name;
        CategoryName = product.CategoryName;
        Barcode = product.Barcode;
        InStock = product.InStock;
    }
}

/// <summary>Товар в топе продаж.</summary>
public class TopProductItem
{
    public int Position { get; }
    public string Name { get; }
    public int Quantity { get; }
    public decimal Revenue { get; }

    public string QuantityLabel => Loc.F("Product_StockPcs", Quantity);

    public TopProductItem(TopProductResponse product, int position)
    {
        Position = position;
        Name = product.Name;
        Quantity = product.Quantity;
        Revenue = product.Revenue;
    }
}
