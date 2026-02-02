using System;
using System.Collections.Generic;
using System.Net.ServerSentEvents;
using System.Text;

namespace Domain.Entities
{
    public class Product
    {
        public long Id { get; set; }
        public string Name { get; set; } = null!;
        public string SKU { get; set; } = null!;
        public string? Barcode { get; set; }
        public string? Description { get; set; }
        public decimal PricePerUnit { get; set; }
        public decimal? CostPerUnit { get; set; }
        public DateTimeOffset CreatedAt { get; set; }

        public long CategoryId { get; set; }   // 🔗 Ссылка на категорию
        public Category Category { get; set; } = null!;

        public Inventory Inventory { get; set; } = null!;
        public ICollection<SaleItem> SaleItems { get; set; } = new List<SaleItem>();
        public ICollection<PurchaseItem> PurchaseItems { get; set; } = new List<PurchaseItem>();
    }
}
