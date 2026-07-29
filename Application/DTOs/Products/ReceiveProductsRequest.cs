using System;
using System.Collections.Generic;
using System.Text;

namespace Application.DTOs.Products
{
    /// <summary>
    /// Партия отсканированных товаров: приход на склад одной покупкой.
    /// </summary>
    public class ReceiveProductsRequest
    {
        public string? SupplierName { get; set; }
        public List<ReceiveItemRequest> Items { get; set; } = new();
    }
}
