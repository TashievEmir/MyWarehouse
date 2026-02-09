using Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.DTOs.Customers
{
    public class CustomerResponse
    {
        public long Id { get; set; }
        public string Name { get; set; } = "";
        public string? Phone { get; set; }
        public string? Email { get; set; }

        public CustomerResponse(Customer c)
        {
            Id = c.Id;
            Name = c.Name;
            Phone = c.Phone;
            Email = c.Email;
        }
    }
}
