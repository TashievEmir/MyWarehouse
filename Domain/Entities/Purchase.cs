using Domain.Exceptions;
using System;
using System.Collections.Generic;
using System.Text;

namespace Domain.Entities
{
    public class Purchase
    {
        public long Id { get; private set; }

        /// <summary>Ссылка на справочник. Может быть пустой у старых поставок.</summary>
        public long? SupplierId { get; private set; }

        /// <summary>Имя на момент прихода: переименование поставщика не переписывает историю.</summary>
        public string SupplierName { get; private set; }

        public DateTimeOffset PurchaseDate { get; private set; }

        private readonly List<PurchaseItem> _items = new();
        public IReadOnlyCollection<PurchaseItem> Items => _items;

        public decimal TotalCost => _items.Sum(x => x.TotalCost);

        private Purchase() { }

        public Purchase(string supplierName, long? supplierId = null)
        {
            if (string.IsNullOrWhiteSpace(supplierName))
                throw new DomainException("Supplier name is required");

            SupplierName = supplierName;
            SupplierId = supplierId;
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
