using System;
using System.Collections.Generic;
using System.Text;

namespace Domain.Entities
{
    public class PurchaseItem
    {
        public long Id { get; private set; }

        public long PurchaseId { get; private set; }
        public Purchase Purchase { get; private set; } = null!;

        public long ProductId { get; private set; }
        public Product Product { get; private set; } = null!;

        public int Quantity { get; private set; }
        public decimal CostPerUnit { get; private set; }

        public decimal TotalCost => Quantity * CostPerUnit;

        private PurchaseItem() { }

        public PurchaseItem(long productId, int quantity, decimal cost)
        {
            ProductId = productId;
            Quantity = quantity;
            CostPerUnit = cost;
        }
    }
}
