﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyWarehouse.Entities
{
    public class Role
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string? Description { get; set; }

        public User User { get; set; }
    }
}
