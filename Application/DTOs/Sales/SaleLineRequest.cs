using System;
using System.Collections.Generic;
using System.Text;

namespace Application.DTOs.Sales
{
    public class SaleLineRequest
    {
        public long ProductId { get; set; }
        public int Quantity { get; set; }
        public decimal Price { get; set; }
    }
}
