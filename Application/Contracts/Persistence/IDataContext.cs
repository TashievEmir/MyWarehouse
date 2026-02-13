using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System;
using System.Collections.Generic;
using System.Data;
using System.Net.ServerSentEvents;
using System.Text;

namespace Application.Contracts.Persistence
{
    public interface IDataContext : IBaseDbContext
    {
        DbSet<Product> Products { get; }
        DbSet<Category> Categories { get; }
        DbSet<Inventory> Inventories { get; }
        DbSet<Sale> Sales { get; }
        DbSet<SaleItem> SaleItems { get; }
        DbSet<Purchase> Purchases { get; }
        DbSet<PurchaseItem> PurchaseItems { get; }
        DbSet<Customer> Customers { get; }
        DbSet<User> Users { get; }
        DbSet<Role> Roles { get; }
        DbSet<DebtPayment> DebtPayments { get; }

        Task<int> SaveChangesAsync(CancellationToken cancellationToken);

        Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken ct);
        Task MigrateAsync(CancellationToken ct);
        
    }
}
