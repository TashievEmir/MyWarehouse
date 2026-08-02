using System.Globalization;
using Application.DTOs.Sales;

using Wpf.Localization;

namespace Wpf.ViewModels.Statistics;

/// <summary>Строка списка долгов.</summary>
public class DebtItem
{
    private static readonly CultureInfo Russian = new("ru-RU");

    public long SaleId { get; }
    public DateTimeOffset SaleDate { get; }

    public string CustomerName { get; }
    public string? CustomerPhone { get; }

    public decimal TotalAmount { get; }
    public decimal PaidAmount { get; }
    public decimal Debt { get; }

    public int PaymentsCount { get; }
    public DateTimeOffset? LastPaymentDate { get; }

    public string SaleNumber => Loc.F("Receipts_Number", SaleId);

    public string DateText => SaleDate.ToLocalTime().ToString("d MMMM yyyy", Russian);

    public string ContactText => string.IsNullOrWhiteSpace(CustomerPhone)
        ? CustomerName
        : $"{CustomerName} · {CustomerPhone}";

    /// <summary>«Оплачено 100 из 165» — видно, гасили долг частично или нет.</summary>
    public string ProgressText => Loc.F("Debt_Progress", PaidAmount, TotalAmount);

    public string PaymentsText => PaymentsCount == 0
        ? Loc.T("Debt_NoPayments")
        : Loc.F("Debt_Payments", PaymentsCount, LastPaymentDate?.ToLocalTime().ToString("dd.MM.yyyy", Loc.Instance.Culture));

    public DebtItem(DebtResponse debt)
    {
        SaleId = debt.SaleId;
        SaleDate = debt.SaleDate;
        CustomerName = debt.CustomerName;
        CustomerPhone = debt.CustomerPhone;
        TotalAmount = debt.TotalAmount;
        PaidAmount = debt.PaidAmount;
        Debt = debt.Debt;
        PaymentsCount = debt.PaymentsCount;
        LastPaymentDate = debt.LastPaymentDate;
    }
}
