using System;
using System.Collections.Generic;
using System.Text;

namespace Domain.Entities
{
    public class Customer
    {
        public long Id { get; set; }
        public string Name { get; set; } = null!;
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public DateTimeOffset CreatedAt { get; set; }

        public ICollection<Sale> Sales { get; set; } = new List<Sale>();
    }
}
