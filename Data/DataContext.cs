using Microsoft.EntityFrameworkCore;
using MyWarehouse.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyWarehouse.Data
{
    public class DataContext : DbContext
    {
        public DataContext()
        {
        }
        public DbSet<User> Users { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<Role> Roles { get; set; }
        public DbSet<Customer> Customers { get; set; }
        public DbSet<Debt> Debts { get; set; }
        public DbSet<Product> Products { get; set; }
        public DbSet<Sale> Sales { get; set; }
        public DbSet<SaleProduct> SaleProducts { get; set; }
        public DbSet<Reference> References { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
        }

        public void InitialValues()
        {
            var hasAccount = Users.Any();
            if (!hasAccount)
            {
                var account = new User
                {
                    Username  = "123",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("123456"),
                    CreatedAt = DateTime.Now,
                    IsActivate = true
                };
                var role = new Role
                {
                    Name = "admin",
                    Description = ""
                };
                account.Role.Add(role);
                Users.Add(account);
                SaveChanges();
            }
            var hasRef = References.Any();
            if (!hasRef)
            {
                var ref1 = new Reference
                {
                    NameRu = "",
                    Code = "1",
                    CreatedAt = DateTime.Now
                };
                var ref1child1 = new Reference
                {
                    NameRu = "",
                    Code = "1",
                    CreatedAt = DateTime.Now,
                    ReferenceParent = ref1
                };
                var ref1child2 = new Reference
                {
                    NameRu = " ",
                    Code = "2",
                    CreatedAt = DateTime.Now,
                    ReferenceParent = ref1
                };
                var ref1child3 = new Reference
                {
                    NameRu = " ",
                    Code = "3",
                    CreatedAt = DateTime.Now,
                    ReferenceParent = ref1
                };
                var ref1child4 = new Reference
                {
                    NameRu = " ",
                    Code = "4",
                    CreatedAt = DateTime.Now,
                    ReferenceParent = ref1
                };
                References.AddRange(new[] { ref1, ref1child1, ref1child2, ref1child3, ref1child4 });
                SaveChanges();
            }
        }
    }
}
