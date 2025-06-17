using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyWarehouse.Entities
{
    public class Debt
    {
        public long DebtPaymentId { get; set; }
        public long SaleId { get; set; }
        public long UserId { get; set; }
        public DateTimeOffset PaymentDate { get; set; }
        public decimal Amount { get; set; }
        public string? Notes { get; set; }

        public Sale Sale { get; set; } = null!;
        public User User { get; set; } = null!;
    }
}
