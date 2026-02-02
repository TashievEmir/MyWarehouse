using System;
using System.Collections.Generic;
using System.Text;

namespace Domain.Entities
{
    public class User
    {
        public long Id { get; set; }
        public string Username { get; set; } = null!;
        public string PasswordHash { get; set; } = null!;
        public string FullName { get; set; } = null!;
        public string Role { get; set; } = "cashier";
        public DateTimeOffset CreatedAt { get; set; }

        public ICollection<Sale> Sales { get; set; } = new List<Sale>();
        public ICollection<DebtPayment> DebtPayments { get; set; } = new List<DebtPayment>();
    }
}
