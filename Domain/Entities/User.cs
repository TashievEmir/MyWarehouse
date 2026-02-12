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
        public string FirstName { get; private set; }
        public string LastName { get; private set; }
        public string? Patronymic { get; private set; }

        private readonly List<UserRole> _roles = new();
        public IReadOnlyCollection<UserRole> Roles => _roles;

        public DateTimeOffset CreatedAt { get; private set; }

        public ICollection<Sale> Sales { get; private set; } = new List<Sale>();
        public ICollection<DebtPayment> DebtPayments { get; private set; } = new List<DebtPayment>();

        private User() { }

        public User(string username, string password, string lastname, string firstname, string patronymic, IEnumerable<int> roleIds)
        {
            if (string.IsNullOrWhiteSpace(username))
                throw new DomainException("Username required");

            if (string.IsNullOrWhiteSpace(password))
                throw new DomainException("Password required");

            if (string.IsNullOrWhiteSpace(firstname))
                throw new DomainException("First name required");

            if (string.IsNullOrWhiteSpace(lastname))
                throw new DomainException("Last name required");

            var roles = roleIds?.ToList() ?? new List<int>();

            if (!roles.Any())
                throw new DomainException("User must have at least one role");

            Username = username;
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password);
            LastName = lastname;
            FirstName = firstname;
            Patronymic = patronymic;
            CreatedAt = DateTimeOffset.UtcNow;

            foreach (var roleId in roles)
            {
                _roles.Add(new UserRole(Id, roleId));
            }
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
