using System;
using System.Collections.Generic;
using System.Text;

namespace Domain.Entities
{
    public class SaleItem
    {
        public long Id { get; set; }

        public long SaleId { get; set; }
        public Sale Sale { get; set; } = null!;

        public long ProductId { get; set; }
        public Product Product { get; set; } = null!;

        public int Quantity { get; set; }
        public decimal PriceAtSale { get; set; }
        public decimal TotalPrice { get; set; }

    }
}
