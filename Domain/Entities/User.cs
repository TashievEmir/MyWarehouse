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

        private readonly List<UserRole> _roles = new();
        public IReadOnlyCollection<UserRole> Roles => _roles;

        public DateTimeOffset CreatedAt { get; private set; }

        public ICollection<Sale> Sales { get; private set; } = new List<Sale>();
        public ICollection<DebtPayment> DebtPayments { get; private set; } = new List<DebtPayment>();

        private User() { }

        public User(string username, string password, string fullName)
        {
            if (string.IsNullOrWhiteSpace(username))
                throw new DomainException("Username required");

            Username = username;
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password);
            FullName = fullName;
            CreatedAt = DateTimeOffset.UtcNow;
        }

        public void AddRole(Role role)
        {
            if (_roles.Any(r => r.RoleId == role.Id))
                return;

            _roles.Add(new UserRole(Id, role.Id));
        }

        public bool VerifyPassword(string password)
        {
            return BCrypt.Net.BCrypt.Verify(password, PasswordHash);
        }
    }
}
