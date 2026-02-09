using System;
using System.Collections.Generic;
using System.Text;

namespace Application.DTOs.Purchases
{
    public class RegisterPurchaseRequest
    {
        public string SupplierName { get; set; } = "";
        public List<PurchaseLineRequest> Items { get; set; } = new();
    }
}
