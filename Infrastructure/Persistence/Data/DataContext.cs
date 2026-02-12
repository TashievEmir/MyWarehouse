using Application.Contracts.Persistence;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System;
using System.Collections.Generic;
using System.Data;
using System.Net.ServerSentEvents;
using System.Reflection.Emit;
using System.Text;

namespace Infrastructure.Persistence.Data
{
    public class DataContext : DbContext, IDataContext
    {
        public DataContext(DbContextOptions<DataContext> options)
        : base(options)
        {
        }

        public DbSet<Product> Products => Set<Product>();
        public DbSet<Category> Categories => Set<Category>();
        public DbSet<Inventory> Inventories => Set<Inventory>();
        public DbSet<Sale> Sales => Set<Sale>();
        public DbSet<SaleItem> SaleItems => Set<SaleItem>();
        public DbSet<Purchase> Purchases => Set<Purchase>();
        public DbSet<PurchaseItem> PurchaseItems => Set<PurchaseItem>();
        public DbSet<Customer> Customers => Set<Customer>();
        public DbSet<User> Users => Set<User>();
        public DbSet<Role> Roles => Set<Role>();
        public DbSet<DebtPayment> DebtPayments => Set<DebtPayment>();

        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken ct)
        => Database.BeginTransactionAsync(ct);

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Category -> Products (1:N)
            //modelBuilder.Entity<Product>()
            //    .HasOne(p => p.Category)
            //    .WithMany(c => c.Products)
            //    .HasForeignKey(p => p.CategoryId)
            //    .OnDelete(DeleteBehavior.Restrict);

            //// Product -> Inventory (1:1)
            //modelBuilder.Entity<Inventory>()
            //    .HasKey(i => i.ProductId);

            //modelBuilder.Entity<Inventory>()
            //    .HasOne(i => i.Product)
            //    .WithOne(p => p.Inventory)
            //    .HasForeignKey<Inventory>(i => i.ProductId);

            //// SaleItem composite key
            //modelBuilder.Entity<SaleItem>()
            //    .HasKey(x => new { x.SaleId, x.ProductId });

            //modelBuilder.Entity<SaleItem>()
            //    .HasOne(x => x.Sale)
            //    .WithMany(s => s.SaleItems)
            //    .HasForeignKey(x => x.SaleId);

            //modelBuilder.Entity<SaleItem>()
            //    .HasOne(x => x.Product)
            //    .WithMany(p => p.SaleItems)
            //    .HasForeignKey(x => x.ProductId);

            //// PurchaseItem composite key
            //modelBuilder.Entity<PurchaseItem>()
            //    .HasKey(x => new { x.PurchaseId, x.ProductId });

            //modelBuilder.Entity<PurchaseItem>()
            //    .HasOne(x => x.Purchase)
            //    .WithMany(p => p.PurchaseItems)
            //    .HasForeignKey(x => x.PurchaseId);

            //modelBuilder.Entity<PurchaseItem>()
            //    .HasOne(x => x.Product)
            //    .WithMany(p => p.PurchaseItems)
            //    .HasForeignKey(x => x.ProductId);

            //// Sale -> Customer (optional)
            //modelBuilder.Entity<Sale>()
            //    .HasOne(s => s.Customer)
            //    .WithMany(c => c.Sales)
            //    .HasForeignKey(s => s.CustomerId)
            //    .OnDelete(DeleteBehavior.SetNull);

            //// User -> Role (N:1)
            //modelBuilder.Entity<User>()
            //    .HasOne(u => u.Role)
            //    .WithMany(r => r.Users)
            //    .HasForeignKey(u => u.RoleId)
            //    .OnDelete(DeleteBehavior.Restrict);

            //// DebtPayment relations
            //modelBuilder.Entity<DebtPayment>()
            //    .HasOne(d => d.Sale)
            //    .WithMany(s => s.DebtPayments)
            //    .HasForeignKey(d => d.SaleId);

            //modelBuilder.Entity<DebtPayment>()
            //    .HasOne(d => d.User)
            //    .WithMany(u => u.DebtPayments)
            //    .HasForeignKey(d => d.UserId);
        }
    }
}
