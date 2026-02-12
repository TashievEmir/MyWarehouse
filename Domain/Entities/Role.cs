using Domain.Exceptions;
using System;
using System.Collections.Generic;
using System.Text;

namespace Domain.Entities
{
    public class Role
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;

        private readonly List<UserRole> _userRoles = new();
        public IReadOnlyCollection<UserRole> UserRoles => _userRoles;

        private Role() { } // EF ctor

        public Role(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new DomainException("Role name is required");

            Name = name;
        }
    }
}
