using System;
using System.Collections.Generic;
using System.Text;

namespace Domain.Entities
{
    public class PurchaseItem
    {
        public long Id { get; set; }

        public long PurchaseId { get; set; }
        public Purchase Purchase { get; set; } = null!;

        public long ProductId { get; set; }
        public Product Product { get; set; } = null!;

        public int Quantity { get; set; }
        public decimal CostPerUnit { get; set; }
    }
}
