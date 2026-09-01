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

        /// <summary>Куда везти товар.</summary>
        public string? Address { get; set; }

        /// <summary>Заметка кассира.</summary>
        public string? Note { get; set; }

        /// <summary>Когда клиента завели — проставляется один раз и не правится.</summary>
        public DateTimeOffset CreatedAt { get; set; }

        public CustomerResponse(Customer c)
        {
            Id = c.Id;
            Name = c.Name;
            Phone = c.Phone;
            Email = c.Email;
            Address = c.Address;
            Note = c.Note;
            CreatedAt = c.CreatedAt;
        }
    }
}
