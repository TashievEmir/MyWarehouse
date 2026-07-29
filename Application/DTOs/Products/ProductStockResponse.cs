using System;
using System.Collections.Generic;
using System.Text;

namespace Application.DTOs.Products
{
    /// <summary>
    /// Товар в статистике: сколько сейчас на складе и сколько поступило за период.
    /// </summary>
    public class ProductStockResponse
    {
        public long ProductId { get; set; }
        public string Name { get; set; } = "";
        public string SKU { get; set; } = "";
        public string? Barcode { get; set; }

        public int InStock { get; set; }
        public int Received { get; set; }
    }
}
