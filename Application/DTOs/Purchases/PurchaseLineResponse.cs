using System;
using System.Collections.Generic;
using System.Text;

namespace Application.DTOs.Purchases
{
    /// <summary>Позиция поставки в журнале закупок.</summary>
    public class PurchaseLineResponse
    {
        public long ProductId { get; set; }
        public string ProductName { get; set; } = "";
        public string? Barcode { get; set; }

        public int Quantity { get; set; }
        public decimal CostPerUnit { get; set; }
        public decimal Total => Quantity * CostPerUnit;
    }
}
