using System;
using System.Collections.Generic;
using System.Text;

namespace Application.DTOs.Customers
{
    public class CreateCustomerRequest
    {
        public string Name { get; set; } = "";
        public string? Phone { get; set; }
        public string? Email { get; set; }

        /// <summary>Куда везти товар.</summary>
        public string? Address { get; set; }

        /// <summary>Заметка кассира.</summary>
        public string? Note { get; set; }
    }
}
