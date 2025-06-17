using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyWarehouse.Entities
{
    public class Sale
    {
        public long Id { get; set; }
        public DateTimeOffset SaleDate { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal PaidAmount { get; set; }
        public bool IsCredit { get; set; }
        public string? PaymentMethod { get; set; }
        public string? Notes { get; set; }

        public long? CustomerId { get; set; }
        public Customer? Customer { get; set; }

        public long UserId { get; set; }
        public User User { get; set; } = null!;
        public ICollection<SaleProduct> SaleProducts { get; set; } = new List<SaleProduct>();
        public ICollection<Debt> Debts { get; set; } = new List<Debt>();
    }
}
