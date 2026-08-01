using System.Globalization;
using Application.DTOs.Sales;
using Domain.Enums;

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

    public string Number => $"Чек №{SaleId}";

    public string TimeText => SaleDate.ToLocalTime().ToString("HH:mm", Russian);

    public string DateText => SaleDate.ToLocalTime().ToString("d MMMM yyyy", Russian);

    public string PositionsText => $"{PositionsCount} поз. · {ItemsCount} шт.";

    public string PaymentName => PaymentLabel.For(PaymentMethod);

    public bool IsCredit => PaymentMethod == PaymentMethod.Credit;

    public ReceiptListItem(ReceiptListItemResponse receipt)
    {
        SaleId = receipt.SaleId;
        SaleDate = receipt.SaleDate;
        CashierName = receipt.CashierName;
        CustomerName = string.IsNullOrWhiteSpace(receipt.CustomerName) ? "без клиента" : receipt.CustomerName!;
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

    public string QuantityText => $"{Quantity} × {Price:N2}";

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
        PaymentMethod.Cash     => "Наличные",
        PaymentMethod.Card     => "Карта",
        PaymentMethod.Transfer => "Перевод",
        PaymentMethod.Credit   => "В долг",
        _                      => "Не указан",
    };
}
