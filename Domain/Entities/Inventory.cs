using System;
using System.Collections.Generic;
using System.Text;

namespace Domain.Entities
{
    public class Inventory
    {
        public long Id { get; set; }
        public int Quantity { get; set; }
        public DateTimeOffset LastUpdated { get; set; }

        public long ProductId { get; set; }
        public Product Product { get; set; } = null!;
    }
}
