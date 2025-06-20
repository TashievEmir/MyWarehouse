﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyWarehouse.Entities
{
    public class SaleProduct
    {
        public long SaleId { get; set; }
        public long ProductId { get; set; }
        public int Quantity { get; set; }
        public decimal PriceAtSale { get; set; }
        public decimal TotalPrice { get; set; }

        public Sale Sale { get; set; } = null!;
        public Product Product { get; set; } = null!;
    }
}
