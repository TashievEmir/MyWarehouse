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

        /// <summary>Цена закупки за штуку.</summary>
        public decimal Cost { get; set; }

        /// <summary>
        /// Цена продажи за штуку — уходит в карточку товара и подставляется на кассе.
        /// Ноль у существующего товара означает «оставить прежнюю цену».
        /// </summary>
        public decimal Price { get; set; }
    }
}
