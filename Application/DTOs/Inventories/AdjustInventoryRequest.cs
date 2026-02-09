using System;
using System.Collections.Generic;
using System.Text;

namespace Application.DTOs.Inventories
{
    public class AdjustInventoryRequest
    {
        public long ProductId { get; set; }
        public int Delta { get; set; } // +increase / -decrease
    }
}
