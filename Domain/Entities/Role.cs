using System;
using System.Collections.Generic;
using System.Text;

namespace Domain.Entities
{
    public class Role
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public string? Description { get; set; }

        public ICollection<User> Users { get; set; } = new List<User>();
    }
}
