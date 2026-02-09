using Domain.Exceptions;
using System;
using System.Collections.Generic;
using System.Text;

namespace Domain.Entities
{
    public class Inventory
    {
        public long Id { get; private set; }

        public long ProductId { get; private set; }
        public Product Product { get; private set; } = null!;

        public int Quantity { get; private set; }
        public DateTimeOffset LastUpdated { get; private set; }

        private Inventory() { }

        public Inventory(long productId, int initialQuantity)
        {
            if (initialQuantity < 0)
                throw new DomainException("Initial quantity cannot be negative");

            ProductId = productId;
            Quantity = initialQuantity;
            LastUpdated = DateTimeOffset.UtcNow;
        }

        public void Increase(int amount)
        {
            if (amount <= 0)
                throw new DomainException("Increase amount must be positive");

            Quantity += amount;
            LastUpdated = DateTimeOffset.UtcNow;
        }

        public void Decrease(int amount)
        {
            if (amount <= 0)
                throw new DomainException("Decrease amount must be positive");

            if (Quantity < amount)
                throw new DomainException("Not enough stock");

            Quantity -= amount;
            LastUpdated = DateTimeOffset.UtcNow;
        }
    }
}
