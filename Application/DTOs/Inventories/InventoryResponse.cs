using Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.DTOs.Inventories
{
    public class InventoryResponse
    {
        public long ProductId { get; set; }
        public int Quantity { get; set; }
        public DateTimeOffset LastUpdated { get; set; }

        public InventoryResponse(Inventory inv)
        {
            ProductId = inv.ProductId;
            Quantity = inv.Quantity;
            LastUpdated = inv.LastUpdated;
        }
    }
}
