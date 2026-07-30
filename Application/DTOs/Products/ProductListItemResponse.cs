using System;
using System.Collections.Generic;
using System.Text;

namespace Application.DTOs.Products
{
    /// <summary>Карточка товара в каталоге.</summary>
    public class ProductListItemResponse
    {
        public long ProductId { get; set; }
        public string Name { get; set; } = "";
        public string SKU { get; set; } = "";
        public string? Barcode { get; set; }
        public string? Description { get; set; }

        public long CategoryId { get; set; }
        public string CategoryName { get; set; } = "";

        public decimal PricePerUnit { get; set; }
        public decimal? CostPerUnit { get; set; }

        public int InStock { get; set; }

        /// <summary>По товару были продажи, поставки или списания — карточку удалять нельзя.</summary>
        public bool HasHistory { get; set; }
    }
}
