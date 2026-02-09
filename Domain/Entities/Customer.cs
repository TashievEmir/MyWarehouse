using Domain.Exceptions;
using System;
using System.Collections.Generic;
using System.Text;

namespace Domain.Entities
{
    public class Customer
    {
        public long Id { get; private set; }

        public string Name { get; private set; }
        public string? Phone { get; private set; }
        public string? Email { get; private set; }

        public DateTimeOffset CreatedAt { get; private set; }

        private Customer() { }

        public Customer(string name, string? phone, string? email)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new DomainException("Customer name is required");

            Name = name;
            Phone = phone;
            Email = email;
            CreatedAt = DateTimeOffset.UtcNow;
        }

        public void Update(string name, string? phone, string? email)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new DomainException("Customer name is required");

            Name = name;
            Phone = phone;
            Email = email;
        }
    }
}
