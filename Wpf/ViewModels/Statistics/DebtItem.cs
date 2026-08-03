using System.Globalization;
using Application.DTOs.Sales;

using Wpf.Localization;

namespace Wpf.ViewModels.Statistics;

/// <summary>Строка списка долгов.</summary>
public class DebtItem
{
    private static CultureInfo Ui => Loc.Instance.Culture;

    public long SaleId { get; }
    public DateTimeOffset SaleDate { get; }

    public string CustomerName { get; }
    public string? CustomerPhone { get; }
    public string? CustomerEmail { get; }

    public decimal TotalAmount { get; }
    public decimal PaidAmount { get; }
    public decimal Debt { get; }

    public int PaymentsCount { get; }
    public DateTimeOffset? LastPaymentDate { get; }

    /// <summary>Обещанный срок погашения. null у долгов, заведённых до появления сроков.</summary>
    public DateTimeOffset? DueDate { get; }

    public string SaleNumber => Loc.F("Receipts_Number", SaleId);

    public string DateText => SaleDate.ToLocalTime().ToString("d MMMM yyyy", Ui);

    public string ContactText => string.IsNullOrWhiteSpace(CustomerPhone)
        ? CustomerName
        : $"{CustomerName} · {CustomerPhone}";

    /// <summary>«Оплачено 100 из 165» — видно, гасили долг частично или нет.</summary>
    public string ProgressText => Loc.F("Debt_Progress", PaidAmount, TotalAmount);

    public string PaymentsText => PaymentsCount == 0
        ? Loc.T("Debt_NoPayments")
        : Loc.F("Debt_Payments", PaymentsCount, LastPaymentDate?.ToLocalTime().ToString("dd.MM.yyyy", Ui));

    // ===================== Срок оплаты =====================

    /// <summary>Сколько дней осталось до срока. Отрицательное — просрочка.</summary>
    public int? DaysLeft => DueDate is null
        ? null
        : (DueDate.Value.ToLocalTime().Date - DateTime.Today).Days;

    /// <summary>Срок прошёл — долг требует внимания и красится в красный.</summary>
    public bool IsOverdue => DaysLeft is < 0;

    public bool IsDueToday => DaysLeft == 0;

    public string DueDateText => DueDate is null
        ? "—"
        : DueDate.Value.ToLocalTime().ToString("dd.MM.yyyy", Ui);

    public string DueStatusText
    {
        get
        {
            if (DaysLeft is not { } days)
                return Loc.T("Debt_NoDueDate");

            if (days == 0) return Loc.T("Debt_DueToday");

            return days < 0
                ? Loc.F("Debt_Overdue", -days)
                : Loc.F("Debt_DueIn", days);
        }
    }

    /// <summary>
    /// Ключ кисти для суммы и срока: до наступления даты долг не красный,
    /// в день срока — предупреждение, после — просрочка.
    /// </summary>
    public string StatusBrushKey => DaysLeft switch
    {
        null   => "MutedBrush",
        < 0    => "DangerBrush",
        0      => "WarningBrush",
        _      => "TextPrimaryBrush",
    };

    public bool HasEmail => !string.IsNullOrWhiteSpace(CustomerEmail);

    /// <summary>Без почты напоминание не уйдёт — это видно прямо в строке.</summary>
    public string EmailText => HasEmail ? CustomerEmail! : Loc.T("Debt_NoEmail");

    public DebtItem(DebtResponse debt)
    {
        SaleId = debt.SaleId;
        SaleDate = debt.SaleDate;
        CustomerName = debt.CustomerName;
        CustomerPhone = debt.CustomerPhone;
        CustomerEmail = debt.CustomerEmail;
        TotalAmount = debt.TotalAmount;
        PaidAmount = debt.PaidAmount;
        Debt = debt.Debt;
        PaymentsCount = debt.PaymentsCount;
        LastPaymentDate = debt.LastPaymentDate;
        DueDate = debt.DueDate;
    }
}
