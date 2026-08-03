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
        /// <summary>Выбранный из справочника поставщик. Пусто — берём по имени.</summary>
        public long? SupplierId { get; set; }

        /// <summary>Имя поставщика. Новое имя попадёт в справочник автоматически.</summary>
        public string? SupplierName { get; set; }

        /// <summary>Кто оформил приход — для истории действий.</summary>
        public long UserId { get; set; }
        public List<ReceiveItemRequest> Items { get; set; } = new();
    }
}
