using Domain.Exceptions;
using System;
using System.Collections.Generic;
using System.Text;

namespace Domain.Entities
{
    public class Category
    {
        public long Id { get; private set; }

        public string Name { get; private set; }
        public string? Description { get; private set; }

        private readonly List<Product> _products = new();
        public IReadOnlyCollection<Product> Products => _products;

        private Category() { }

        public Category(string name, string? description)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new DomainException("Category name is required");

            Name = name;
            Description = description;
        }

        public void Update(string name, string? description)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new DomainException("Category name is required");

            Name = name;
            Description = description;
        }
    }
}
