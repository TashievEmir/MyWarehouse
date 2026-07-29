using System;
using System.Collections.Generic;
using System.Text;

namespace Application.DTOs.Products
{
    /// <summary>
    /// Категория со сводкой «есть / поступило» и списком товаров внутри.
    /// </summary>
    public class CategoryStockResponse
    {
        public long CategoryId { get; set; }
        public string Name { get; set; } = "";

        public int InStock { get; set; }
        public int Received { get; set; }

        public List<ProductStockResponse> Products { get; set; } = new();
    }
}
