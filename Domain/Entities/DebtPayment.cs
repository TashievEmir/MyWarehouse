using Domain.Exceptions;
using System;
using System.Collections.Generic;
using System.Text;

namespace Domain.Entities
{
    public class DebtPayment
    {
        public long Id { get; private set; }

        public long SaleId { get; private set; }
        public Sale Sale { get; private set; } = null!;

        public long UserId { get; private set; }
        public User User { get; private set; } = null!;

        public DateTimeOffset PaymentDate { get; private set; }
        public decimal Amount { get; private set; }
        public string? Notes { get; private set; }

        private DebtPayment() { } // EF ctor

        public DebtPayment(long saleId, long userId, decimal amount, string? notes = null)
        {
            if (amount <= 0)
                throw new DomainException("Payment amount must be positive");

            SaleId = saleId;
            UserId = userId;
            Amount = amount;
            Notes = notes;
            PaymentDate = DateTimeOffset.UtcNow;
        }
    }
}
