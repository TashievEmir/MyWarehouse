using System;
using System.Collections.Generic;
using System.Text;

namespace Domain.Entities
{
    public class Purchase
    {
        public long Id { get; set; }
        public DateTimeOffset PurchaseDate { get; set; }
        public string SupplierName { get; set; } = null!;
        public decimal TotalCost { get; set; }

        public ICollection<PurchaseItem> PurchaseItems { get; set; } = new List<PurchaseItem>();
    }
}
