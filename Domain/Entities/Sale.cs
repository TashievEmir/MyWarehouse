using Domain.Exceptions;

namespace Domain.Entities
{
    public class Sale
    {
        public long Id { get; private set; }

        public long? CustomerId { get; private set; }
        public long UserId { get; private set; }

        public DateTimeOffset SaleDate { get; private set; }

        public decimal TotalAmount { get; private set; }
        public decimal PaidAmount { get; private set; }

        public bool IsCredit => PaidAmount < TotalAmount;

        private readonly List<SaleItem> _items = new();
        public IReadOnlyCollection<SaleItem> SaleItems => _items;

        private readonly List<DebtPayment> _payments = new();
        public IReadOnlyCollection<DebtPayment> DebtPayments => _payments;

        public Sale(long? customerId, long userId)
        {
            CustomerId = customerId;
            UserId = userId;
            SaleDate = DateTimeOffset.UtcNow;
        }

        public void AddItem(long productId, int quantity, decimal price)
        {
            if (quantity <= 0)
                throw new DomainException("Quantity must be positive");

            var item = new SaleItem(productId, quantity, price);
            _items.Add(item);

            TotalAmount += item.TotalPrice;
        }

        public void Pay(decimal amount)
        {
            if (amount <= 0)
                throw new DomainException("Payment must be positive");

            PaidAmount += amount;
        }
    }
}
