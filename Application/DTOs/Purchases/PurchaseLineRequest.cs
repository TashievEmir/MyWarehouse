using System;
using System.Collections.Generic;
using System.Text;

namespace Application.DTOs.Purchases
{
    public class PurchaseLineRequest
    {
        public long ProductId { get; set; }
        public int Quantity { get; set; }
        public decimal Cost { get; set; }
    }
}
