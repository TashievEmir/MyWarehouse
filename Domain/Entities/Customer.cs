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

        /// <summary>Куда везти товар.</summary>
        public string? Address { get; private set; }

        /// <summary>Заметка кассира: «звонить после обеда», «платит с задержкой».</summary>
        public string? Note { get; private set; }

        public DateTimeOffset CreatedAt { get; private set; }

        private Customer() { }

        public Customer(string name, string? phone, string? email, string? address = null, string? note = null)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new DomainException("Customer name is required");

            Name = name;
            Phone = phone;
            Email = email;
            Address = Clean(address);
            Note = Clean(note);
            CreatedAt = DateTimeOffset.UtcNow;
        }

        public void Update(string name, string? phone, string? email, string? address = null, string? note = null)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new DomainException("Customer name is required");

            Name = name;
            Phone = phone;
            Email = email;
            Address = Clean(address);
            Note = Clean(note);
        }

        /// <summary>Пустое поле храним как null, а не как пустую строку — так проще проверять.</summary>
        private static string? Clean(string? value)
            => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
