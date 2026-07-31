using System;
using System.Collections.Generic;
using System.Text;

namespace Application.DTOs.Purchases
{
    /// <summary>Поставка в журнале закупок вместе с составом.</summary>
    public class PurchaseListItemResponse
    {
        public long PurchaseId { get; set; }
        public string SupplierName { get; set; } = "";
        public DateTimeOffset PurchaseDate { get; set; }

        public int PositionsCount { get; set; }
        public int ItemsCount { get; set; }
        public decimal TotalCost { get; set; }

        public List<PurchaseLineResponse> Lines { get; set; } = new();
    }
}
