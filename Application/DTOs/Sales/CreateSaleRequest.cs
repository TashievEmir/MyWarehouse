using System;
using System.Collections.Generic;
using System.Text;

namespace Application.DTOs.Sales
{
    public class CreateSaleRequest
    {
        public long? CustomerId { get; set; }
        public long UserId { get; set; }
        public decimal PaidAmount { get; set; }

        public List<SaleLineRequest> Items { get; set; } = new();
    }
}
