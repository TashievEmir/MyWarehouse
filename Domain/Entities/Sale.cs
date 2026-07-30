using Domain.Enums;
using Domain.Exceptions;

namespace Domain.Entities
{
    public class Sale
    {
        public long Id { get; private set; }

        public long? CustomerId { get; private set; }
        public long UserId { get; private set; }

        public DateTimeOffset SaleDate { get; private set; }

        /// <summary>Сумма позиций до скидки.</summary>
        public decimal Subtotal { get; private set; }

        public decimal DiscountAmount { get; private set; }

        /// <summary>К оплате: сумма позиций минус скидка.</summary>
        public decimal TotalAmount { get; private set; }

        public decimal PaidAmount { get; private set; }

        public PaymentMethod PaymentMethod { get; private set; }

        public bool IsCredit => PaidAmount < TotalAmount;

        private readonly List<SaleItem> _items = new();
        public IReadOnlyCollection<SaleItem> SaleItems => _items;

        private readonly List<DebtPayment> _payments = new();
        public IReadOnlyCollection<DebtPayment> DebtPayments => _payments;

        private Sale() { }

        public Sale(long? customerId, long userId)
        {
            CustomerId = customerId;
            UserId = userId;
            SaleDate = DateTimeOffset.UtcNow;
            PaymentMethod = PaymentMethod.Cash;
        }

        public void AddItem(long productId, int quantity, decimal price)
        {
            if (quantity <= 0)
                throw new DomainException("Quantity must be positive");

            var item = new SaleItem(productId, quantity, price);
            _items.Add(item);

            Subtotal += item.TotalPrice;

            Recalculate();
        }

        /// <summary>Скидка на весь чек, в деньгах.</summary>
        public void ApplyDiscount(decimal amount)
        {
            if (amount < 0)
                throw new DomainException("Discount cannot be negative");

            if (amount > Subtotal)
                throw new DomainException("Discount cannot exceed the subtotal");

            DiscountAmount = amount;

            Recalculate();
        }

        public void SetPaymentMethod(PaymentMethod method)
        {
            if (method == PaymentMethod.Unknown)
                throw new DomainException("Payment method is required");

            PaymentMethod = method;
        }

        public void Pay(decimal amount)
        {
            if (amount <= 0)
                throw new DomainException("Payment must be positive");

            if (PaidAmount + amount > TotalAmount)
                throw new DomainException("Payment exceeds the amount due");

            PaidAmount += amount;
        }

        private void Recalculate() => TotalAmount = Subtotal - DiscountAmount;
    }
}
