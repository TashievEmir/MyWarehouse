using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyWarehouse.Entities
{
    public class Customer
    {
        public long Id { get; set; }
        public string Name { get; set; } = null!;
        public string Surname { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset LastUpdated { get; set; }

        public ICollection<Sale> Sales { get; set; } = new List<Sale>();
    }
}
