using System;
using System.Collections.Generic;
using System.Net.ServerSentEvents;
using System.Text;

namespace Domain.Entities
{
    public class Sale
    {
        public long Id { get; set; }

        public long? CustomerId { get; set; }
        public Customer? Customer { get; set; }


        public long UserId { get; set; }
        public User User { get; set; } = null!;

        public DateTimeOffset SaleDate { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal PaidAmount { get; set; }
        public bool IsCredit { get; set; }
        public string? PaymentMethod { get; set; }
        public string? Notes { get; set; }


        public ICollection<SaleItem> SaleItems { get; set; } = new List<SaleItem>();
        public ICollection<DebtPayment> DebtPayments { get; set; } = new List<DebtPayment>();
    }
}
