using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyWarehouse.Entities
{
    public class Account
    {
        public long Id { get; set; }
        public string Username { get; set; } = null!;
        public string PasswordHash { get; set; } = null!;
        public string FullName { get; set; } = null!;
        public int Role { get; set; }
        public DateTimeOffset CreatedAt { get; set; }

        //public ICollection<Sale> Sales { get; set; } = new List<Sale>();
        //public ICollection<DebtPayment> DebtPayments { get; set; } = new List<DebtPayment>();
    }
}
