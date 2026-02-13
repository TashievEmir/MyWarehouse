using Domain.Exceptions;

namespace Domain.Entities
{
    public class SaleItem
    {
        public long Id { get; private set; }

        public long SaleId { get; private set; }
        public Sale Sale { get; private set; } = null!;

        public long ProductId { get; private set; }
        public Product Product { get; private set; } = null!;

        public int Quantity { get; private set; }
        public decimal PriceAtSale { get; private set; }

        public decimal TotalPrice => Quantity * PriceAtSale;

        private SaleItem()
        {
        }

        public SaleItem(long productId, int quantity, decimal price)
        {
            if (quantity <= 0)
                throw new DomainException("Quantity must be positive");

            if (price < 0)
                throw new DomainException("Price cannot be negative");

            ProductId = productId;
            Quantity = quantity;
            PriceAtSale = price;

        }
    }
}
