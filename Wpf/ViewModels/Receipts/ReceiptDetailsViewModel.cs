using System.Collections.ObjectModel;
using System.Globalization;
using Application.DTOs.Sales;
using Domain.Enums;
using Wpf.Common;

namespace Wpf.ViewModels.Receipts;

/// <summary>Просмотр чека: шапка, позиции и итоги — как на печатной ленте.</summary>
public class ReceiptDetailsViewModel : ViewModelBase
{
    private static readonly CultureInfo Russian = new("ru-RU");

    public long SaleId { get; }

    public string Number { get; }
    public string SubTitle { get; }

    public ObservableCollection<ReceiptLineItem> Lines { get; } = new();

    public decimal Subtotal { get; }
    public decimal DiscountAmount { get; }
    public decimal TotalAmount { get; }
    public decimal PaidAmount { get; }
    public decimal DebtLeft { get; }

    public bool HasDiscount => DiscountAmount > 0;

    public string DiscountLabel => Subtotal <= 0
        ? "Скидка"
        : $"Скидка {DiscountAmount / Subtotal * 100:0.#} %";

    public string PaymentName { get; }

    public bool IsCredit { get; }

    public string CustomerName { get; }

    public bool HasCustomer => CustomerName.Length > 0;

    public ReceiptDetailsViewModel(ReceiptDetailsResponse receipt)
    {
        SaleId = receipt.SaleId;

        Number = $"Чек №{receipt.SaleId}";
        SubTitle = $"{receipt.SaleDate.ToLocalTime().ToString("d MMMM yyyy, HH:mm", Russian)} · {receipt.CashierName}";

        foreach (var line in receipt.Lines)
            Lines.Add(new ReceiptLineItem(line));

        Subtotal = receipt.Subtotal;
        DiscountAmount = receipt.DiscountAmount;
        TotalAmount = receipt.TotalAmount;
        PaidAmount = receipt.PaidAmount;
        DebtLeft = receipt.DebtLeft;

        PaymentName = PaymentLabel.For(receipt.PaymentMethod);
        IsCredit = receipt.PaymentMethod == PaymentMethod.Credit;

        CustomerName = receipt.CustomerName ?? "";
    }
}
