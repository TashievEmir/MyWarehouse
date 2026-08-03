using Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.DTOs.Sales
{
    /// <summary>Незакрытый долг по продаже: сколько осталось получить с клиента.</summary>
    public class DebtResponse
    {
        public long SaleId { get; set; }
        public DateTimeOffset SaleDate { get; set; }

        public long? CustomerId { get; set; }
        public string CustomerName { get; set; } = "";
        public string? CustomerPhone { get; set; }
        public string? CustomerEmail { get; set; }

        public decimal TotalAmount { get; set; }
        public decimal PaidAmount { get; set; }
        public decimal Debt => TotalAmount - PaidAmount;

        public PaymentMethod PaymentMethod { get; set; }

        /// <summary>Сколько раз уже вносили деньги по этому долгу.</summary>
        public int PaymentsCount { get; set; }

        public DateTimeOffset? LastPaymentDate { get; set; }

        /// <summary>Обещанный срок погашения. null у долгов, заведённых до появления сроков.</summary>
        public DateTimeOffset? DueDate { get; set; }
    }
}
