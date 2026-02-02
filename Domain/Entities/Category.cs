using System;
using System.Collections.Generic;
using System.Text;

namespace Domain.Entities
{
    public class Category
    {
        public long Id { get; set; }
        public string Name { get; set; } = null!;
        public string? Description { get; set; }

        public ICollection<Product> Products { get; set; } = new List<Product>();
    }
}
