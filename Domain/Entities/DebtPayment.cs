using System;
using System.Collections.Generic;
using System.Text;

namespace Domain.Entities
{
    public class DebtPayment
    {
        public long Id { get; set; }

        public long SaleId { get; set; }
        public Sale Sale { get; set; } = null!;

        public long UserId { get; set; }
        public User User { get; set; } = null!;

        public DateTimeOffset PaymentDate { get; set; }
        public decimal Amount { get; set; }
        public string? Notes { get; set; }
    }
}
