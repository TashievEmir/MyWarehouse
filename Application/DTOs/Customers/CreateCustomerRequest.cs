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
    }
}
