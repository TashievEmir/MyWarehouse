using Application.Contracts.Persistence;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

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
        public DbSet<Supplier> Suppliers => Set<Supplier>();
        public DbSet<Customer> Customers => Set<Customer>();
        public DbSet<User> Users => Set<User>();
        public DbSet<Role> Roles => Set<Role>();
        public DbSet<DebtPayment> DebtPayments => Set<DebtPayment>();
        public DbSet<StockWriteOff> StockWriteOffs => Set<StockWriteOff>();
        public DbSet<ActivityLogEntry> ActivityLog => Set<ActivityLogEntry>();
        public DbSet<ReceiptTemplate> ReceiptTemplates => Set<ReceiptTemplate>();

        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken ct)
        => Database.BeginTransactionAsync(ct);
        
        public async Task MigrateAsync(CancellationToken ct)
        {
            await Database.MigrateAsync(ct);
        }
        

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // SaleItem composite key
            modelBuilder.Entity<SaleItem>()
                .HasKey(x => new { x.SaleId, x.ProductId });

            // PurchaseItem composite key
            modelBuilder.Entity<PurchaseItem>()
                .HasKey(x => new { x.PurchaseId, x.ProductId });

            // Штрихкод определяет товар: две карточки с одним кодом развели бы
            // остатки по разным товарам. NULL в SQLite между собой не конфликтуют,
            // поэтому товары без штрихкода индекс не задевает.
            modelBuilder.Entity<Product>()
                .HasIndex(p => p.Barcode)
                .IsUnique();

            // Один поставщик — одна запись справочника, иначе список на приёмке
            // зарастёт дублями «Абдылда» / «абдылда»
            modelBuilder.Entity<Supplier>()
                .HasIndex(s => s.Name)
                .IsUnique();

            // Поставка ссылается на справочник, но имя хранит своё:
            // удаление поставщика не должно стирать журнал закупок
            modelBuilder.Entity<Purchase>()
                .HasOne<Supplier>()
                .WithMany()
                .HasForeignKey(p => p.SupplierId)
                .OnDelete(DeleteBehavior.SetNull);

            // Inventory 1:1 Product
            modelBuilder.Entity<Inventory>()
                .HasOne(i => i.Product)
                .WithOne(p => p.Inventory)
                .HasForeignKey<Inventory>(i => i.ProductId);

            // Many-to-many User ↔ Role
            modelBuilder.Entity<UserRole>()
                .HasKey(x => new { x.UserId, x.RoleId });

            // User ↔ UserRole
            modelBuilder.Entity<UserRole>()
                .HasOne(x => x.User)
                .WithMany(u => u.Roles)
                .HasForeignKey(x => x.UserId);

            // Role ↔ UserRole
            modelBuilder.Entity<UserRole>()
                .HasOne(x => x.Role)
                .WithMany(r => r.UserRoles)
                .HasForeignKey(x => x.RoleId);
        }
    }
}
