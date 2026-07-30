using Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.DTOs.Products
{
    /// <summary>Списание товара со склада с указанием количества и причины.</summary>
    public class WriteOffProductRequest
    {
        public long ProductId { get; set; }
        public long UserId { get; set; }

        public int Quantity { get; set; }

        public WriteOffReason Reason { get; set; } = WriteOffReason.Damage;
        public string? Comment { get; set; }
    }
}
