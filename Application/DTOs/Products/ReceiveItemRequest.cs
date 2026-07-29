using System;
using System.Collections.Generic;
using System.Text;

namespace Application.DTOs.Products
{
    /// <summary>
    /// Одна позиция прихода. Если <see cref="ProductId"/> не задан — товар заводится
    /// по штрихкоду, названию и категории.
    /// </summary>
    public class ReceiveItemRequest
    {
        public long? ProductId { get; set; }

        public string? Barcode { get; set; }
        public string? Name { get; set; }
        public long? CategoryId { get; set; }

        public int Quantity { get; set; }
        public decimal Cost { get; set; }
    }
}
