using Domain.Exceptions;
using System;
using System.Collections.Generic;
using System.Text;

namespace Domain.Entities
{
    public class User
    {
        public long Id { get; private set; }

        public string Username { get; private set; }
        public string PasswordHash { get; private set; }
        public string FullName { get; private set; }

        public string Role { get; private set; }

        public DateTimeOffset CreatedAt { get; private set; }

        public ICollection<Sale> Sales { get; private set; } = new List<Sale>();
        public ICollection<DebtPayment> DebtPayments { get; private set; } = new List<DebtPayment>();

        private User() { } // EF ctor

        public User(string username, string password, string fullName, string role)
        {
            if (string.IsNullOrWhiteSpace(username))
                throw new DomainException("Username required");

            Username = username;
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password);
            FullName = fullName;
            Role = role;
            CreatedAt = DateTimeOffset.UtcNow;
        }

        public bool VerifyPassword(string password)
        {
            return BCrypt.Net.BCrypt.Verify(password, PasswordHash);
        }
    }
}
