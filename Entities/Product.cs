using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyWarehouse.Entities
{
    public class Product
    {
        public long Id { get; set; }
        public string Name { get; set; } = null!;
        public string SKU { get; set; } = null!;
        public string Barcode { get; set; }
        public string? Description { get; set; }
        public decimal PricePerUnit { get; set; }
        public decimal CostPerUnit { get; set; }
        public long Quantity { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset LastUpdated { get; set; }

        public long CategoryId { get; set; }
        public Category Category { get; set; } = null!;

        public ICollection<SaleProduct> SaleProducts { get; set; } = new List<SaleProduct>();
    }
}
