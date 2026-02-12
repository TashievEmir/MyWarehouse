using System;
using System.Collections.Generic;
using System.Text;

namespace Domain.Entities
{
    public class UserRole
    {
        public long UserId { get; private set; }
        public User User { get; private set; } = null!;

        public int RoleId { get; private set; }
        public Role Role { get; private set; } = null!;

        private UserRole() { }

        public UserRole(long userId, int roleId)
        {
            UserId = userId;
            RoleId = roleId;
        }
    }
}
