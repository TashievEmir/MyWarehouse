using Domain.Exceptions;
using System;
using System.Collections.Generic;
using System.Text;

namespace Domain.Entities
{
    public class Purchase
    {
        public long Id { get; private set; }

        public string SupplierName { get; private set; }
        public DateTimeOffset PurchaseDate { get; private set; }

        private readonly List<PurchaseItem> _items = new();
        public IReadOnlyCollection<PurchaseItem> Items => _items;

        public decimal TotalCost => _items.Sum(x => x.TotalCost);

        private Purchase() { }

        public Purchase(string supplierName)
        {
            if (string.IsNullOrWhiteSpace(supplierName))
                throw new DomainException("Supplier name is required");

            SupplierName = supplierName;
            PurchaseDate = DateTimeOffset.UtcNow;
        }

        public void AddItem(long productId, int quantity, decimal cost)
        {
            if (quantity <= 0)
                throw new DomainException("Quantity must be positive");

            if (cost < 0)
                throw new DomainException("Cost cannot be negative");

            _items.Add(new PurchaseItem(productId, quantity, cost));
        }
    }
}
