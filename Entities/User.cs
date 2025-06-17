using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyWarehouse.Entities
{
    public class User
    {
        public long Id { get; set; }
        public string Username { get; set; } = null!;
        public string PasswordHash { get; set; } = null!;
        public string FullName { get; set; } = null!;
        public bool IsActivate { get; set; }
        public DateTimeOffset CreatedAt { get; set; }

        public int RoleId { get; set; }
        public Role Role { get; set; }

        public ICollection<Sale> Sales { get; set; } = new List<Sale>();
        public ICollection<Debt> Debts { get; set; } = new List<Debt>();
    }
}
