using Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.DTOs.Sales
{
    public class SaleItemResponse
    {
        public long ProductId { get; set; }
        public int Quantity { get; set; }
        public decimal Price { get; set; }
        public decimal Total => Quantity * Price;

        public SaleItemResponse() { }

        public SaleItemResponse(SaleItem item)
        {
            ProductId = item.ProductId;
            Quantity = item.Quantity;
            Price = item.PriceAtSale;
        }
    }
}
