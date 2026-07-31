using System.Collections.ObjectModel;
using System.Globalization;
using Application.DTOs.Purchases;
using Wpf.Common;

namespace Wpf.ViewModels.Statistics;

/// <summary>Позиция поставки в журнале закупок.</summary>
public class PurchaseLineItem
{
    public string ProductName { get; }
    public string? Barcode { get; }
    public int Quantity { get; }
    public decimal CostPerUnit { get; }
    public decimal Total { get; }

    public string QuantityText => $"{Quantity} шт. × {CostPerUnit:N2}";

    public PurchaseLineItem(PurchaseLineResponse line)
    {
        ProductName = line.ProductName;
        Barcode = line.Barcode;
        Quantity = line.Quantity;
        CostPerUnit = line.CostPerUnit;
        Total = line.Total;
    }
}

/// <summary>Поставка в журнале: шапка со сводкой и раскрывающийся состав.</summary>
public class PurchaseLogItem : ViewModelBase
{
    private static readonly CultureInfo Russian = new("ru-RU");

    public long PurchaseId { get; }
    public string SupplierName { get; }
    public DateTimeOffset PurchaseDate { get; }

    public int PositionsCount { get; }
    public int ItemsCount { get; }
    public decimal TotalCost { get; }

    public ObservableCollection<PurchaseLineItem> Lines { get; } = new();

    public string Number => $"Поставка №{PurchaseId}";

    public string DateText => PurchaseDate.ToLocalTime().ToString("d MMMM yyyy, HH:mm", Russian);

    public string SummaryText => $"{PositionsCount} поз. · {ItemsCount} шт.";

    private bool _isExpanded;
    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetProperty(ref _isExpanded, value);
    }

    public PurchaseLogItem(PurchaseListItemResponse purchase)
    {
        PurchaseId = purchase.PurchaseId;
        SupplierName = purchase.SupplierName;
        PurchaseDate = purchase.PurchaseDate;
        PositionsCount = purchase.PositionsCount;
        ItemsCount = purchase.ItemsCount;
        TotalCost = purchase.TotalCost;

        foreach (var line in purchase.Lines.OrderBy(l => l.ProductName))
            Lines.Add(new PurchaseLineItem(line));
    }
}
