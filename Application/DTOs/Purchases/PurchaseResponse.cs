using Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.DTOs.Purchases
{
    public class PurchaseResponse
    {
        public long Id { get; set; }
        public string SupplierName { get; set; } = "";
        public DateTimeOffset Date { get; set; }
        public decimal TotalCost { get; set; }

        public PurchaseResponse(Purchase purchase)
        {
            Id = purchase.Id;
            SupplierName = purchase.SupplierName;
            Date = purchase.PurchaseDate;
            TotalCost = purchase.TotalCost;
        }
    }
}
