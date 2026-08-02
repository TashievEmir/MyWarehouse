using System.Globalization;
using Application.DTOs.Sales;
using Domain.Enums;

using Wpf.Localization;

namespace Wpf.ViewModels.Receipts;

/// <summary>Строка списка чеков.</summary>
public class ReceiptListItem
{
    private static readonly CultureInfo Russian = new("ru-RU");

    public long SaleId { get; }
    public DateTimeOffset SaleDate { get; }

    public string CashierName { get; }
    public string CustomerName { get; }

    public int PositionsCount { get; }
    public int ItemsCount { get; }

    public PaymentMethod PaymentMethod { get; }
    public decimal TotalAmount { get; }

    public string Number => Loc.F("Receipts_Number", SaleId);

    public string TimeText => SaleDate.ToLocalTime().ToString("HH:mm", Russian);

    public string DateText => SaleDate.ToLocalTime().ToString("d MMMM yyyy", Russian);

    public string PositionsText => Loc.F("Receipts_Positions", PositionsCount, ItemsCount);

    public string PaymentName => PaymentLabel.For(PaymentMethod);

    public bool IsCredit => PaymentMethod == PaymentMethod.Credit;

    /// <summary>Чек отменён возвратом.</summary>
    public bool IsReturned { get; }

    public ReceiptListItem(ReceiptListItemResponse receipt)
    {
        IsReturned = receipt.IsReturned;
        SaleId = receipt.SaleId;
        SaleDate = receipt.SaleDate;
        CashierName = receipt.CashierName;
        CustomerName = string.IsNullOrWhiteSpace(receipt.CustomerName) ? Loc.T("Receipts_NoCustomer") : receipt.CustomerName!;
        PositionsCount = receipt.PositionsCount;
        ItemsCount = receipt.ItemsCount;
        PaymentMethod = receipt.PaymentMethod;
        TotalAmount = receipt.TotalAmount;
    }
}

/// <summary>Позиция в просмотре чека.</summary>
public class ReceiptLineItem
{
    public string ProductName { get; }
    public int Quantity { get; }
    public decimal Price { get; }
    public decimal Total { get; }

    public string QuantityText => Loc.F("Receipts_LineQuantity", Quantity, Price);

    public ReceiptLineItem(ReceiptLineResponse line)
    {
        ProductName = line.ProductName;
        Quantity = line.Quantity;
        Price = line.Price;
        Total = line.Total;
    }
}

/// <summary>Человеческие подписи способов оплаты.</summary>
public static class PaymentLabel
{
    public static string For(PaymentMethod method) => method switch
    {
        PaymentMethod.Cash     => Loc.T("Payment_Cash"),
        PaymentMethod.Card     => Loc.T("Payment_Card"),
        PaymentMethod.Transfer => Loc.T("Payment_Transfer"),
        PaymentMethod.Credit   => Loc.T("Payment_Credit"),
        _                      => Loc.T("Payment_Unknown"),
    };
}
