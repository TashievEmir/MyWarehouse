using System;
using System.Collections.Generic;
using System.Text;

namespace Application.DTOs.Products
{
    /// <summary>
    /// Результат поиска товара по штрихкоду при сканировании.
    /// </summary>
    public class ProductLookupResponse
    {
        public long ProductId { get; set; }
        public string Name { get; set; } = "";
        public string SKU { get; set; } = "";
        public string? Barcode { get; set; }

        public long CategoryId { get; set; }
        public string CategoryName { get; set; } = "";

        public int InStock { get; set; }

        /// <summary>Цена продажи из карточки товара.</summary>
        public decimal PricePerUnit { get; set; }

        public decimal? CostPerUnit { get; set; }
    }
}
