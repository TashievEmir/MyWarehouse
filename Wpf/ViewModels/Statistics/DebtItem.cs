using System.Globalization;
using Application.DTOs.Sales;

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

    public string SaleNumber => $"Чек №{SaleId}";

    public string DateText => SaleDate.ToLocalTime().ToString("d MMMM yyyy", Russian);

    public string ContactText => string.IsNullOrWhiteSpace(CustomerPhone)
        ? CustomerName
        : $"{CustomerName} · {CustomerPhone}";

    /// <summary>«Оплачено 100 из 165» — видно, гасили долг частично или нет.</summary>
    public string ProgressText => $"оплачено {PaidAmount:N2} из {TotalAmount:N2}";

    public string PaymentsText => PaymentsCount == 0
        ? "платежей не было"
        : $"платежей: {PaymentsCount}, последний {LastPaymentDate?.ToLocalTime():dd.MM.yyyy}";

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
