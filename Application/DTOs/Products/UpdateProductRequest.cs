using System;
using System.Collections.Generic;
using System.Text;

namespace Application.DTOs.Products
{
    public class UpdateProductRequest
    {
        public long ProductId { get; set; }

        public string Name { get; set; } = "";
        public string SKU { get; set; } = "";
        public string? Barcode { get; set; }
        public string? Description { get; set; }

        public long CategoryId { get; set; }

        public decimal PricePerUnit { get; set; }
        public decimal? CostPerUnit { get; set; }
    }
}
