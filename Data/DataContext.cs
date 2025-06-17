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
            modelBuilder.Entity<SaleProduct>()
                .HasKey(x => new { x.SaleId, x.ProductId });
        }

        public void InitialValues()
        {
            var hasRole = Roles.Any();
            if (!hasRole)
            {
                var role = new Role
                {
                    Name = "superadmin",
                    Description = "access to whole project"
                };
                Roles.Add(role);
                SaveChanges();
            }

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
                account.Role = role;
                Users.Add(account);
                SaveChanges();
            }
            
        }
    }
}
