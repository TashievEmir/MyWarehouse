using System.Collections.ObjectModel;
using System.Globalization;
using Application.DTOs.Sales;
using Domain.Enums;
using Wpf.Common;

using Wpf.Localization;

namespace Wpf.ViewModels.Receipts;

/// <summary>Просмотр чека: шапка, позиции и итоги — как на печатной ленте.</summary>
public class ReceiptDetailsViewModel : ViewModelBase
{
    private static CultureInfo Ui => Wpf.Localization.Loc.Instance.Culture;

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
        ? Loc.T("Receipts_DiscountLabel")
        : Loc.F("Receipts_DiscountPercent", (DiscountAmount / Subtotal * 100).ToString("0.#", Ui));

    /// <summary>Строка «Долг · Имя» в ленте чека.</summary>
    public string DebtLabel => Loc.F("Receipts_DebtLine", CustomerName);

    public string PaymentName { get; }

    public bool IsCredit { get; }

    public string CustomerName { get; }

    public bool HasCustomer => CustomerName.Length > 0;

    /// <summary>Чек отменён возвратом — вернуть его второй раз нельзя.</summary>
    public bool IsReturned { get; }

    public bool CanReturn => !IsReturned;

    /// <summary>Лента чека текстом — для копии в буфер обмена.</summary>
    public string AsText()
    {
        var text = new System.Text.StringBuilder();

        text.AppendLine(Number);
        text.AppendLine(SubTitle);
        text.AppendLine(new string('-', 32));

        foreach (var line in Lines)
            text.AppendLine($"{line.ProductName}  {line.QuantityText} = {line.Total:N2}");

        text.AppendLine(new string('-', 32));
        text.AppendLine(Loc.F("Receipt_Copy_Subtotal", Subtotal.ToString("N2", Ui)));

        if (HasDiscount)
            text.AppendLine(Loc.F("Receipt_Copy_Discount", DiscountLabel, DiscountAmount.ToString("N2", Ui)));

        text.AppendLine(Loc.F("Receipt_Copy_Total", TotalAmount.ToString("N2", Ui)));
        text.AppendLine($"{PaymentName}: {PaidAmount:N2}");

        if (IsCredit)
            text.AppendLine(Loc.F("Receipt_Copy_Debt", DebtLeft.ToString("N2", Ui)));

        if (IsReturned)
            text.AppendLine(Loc.T("Receipts_Returned"));

        return text.ToString();
    }

    public ReceiptDetailsViewModel(ReceiptDetailsResponse receipt)
    {
        SaleId = receipt.SaleId;

        Number = Loc.F("Receipts_Number", receipt.SaleId);
        SubTitle = $"{receipt.SaleDate.ToLocalTime().ToString("d MMMM yyyy, HH:mm", Ui)} · {receipt.CashierName}";

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
        IsReturned = receipt.IsReturned;
    }
}
