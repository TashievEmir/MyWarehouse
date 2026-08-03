using Application.Contracts.Interfaces;
using Application.Contracts.Persistence;
using Application.DTOs.Suppliers;
using Application.Localization;
using Domain.Entities;
using Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Application.Services
{
    /// <summary>
    /// Справочник поставщиков. Наполняется сам: любое имя, введённое на приёмке,
    /// попадает сюда и в следующий раз доступно в выпадающем списке.
    /// </summary>
    public class SupplierService : ISupplierService
    {
        private readonly IDataContext _db;

        public SupplierService(IDataContext db)
        {
            _db = db;
        }

        public async Task<List<SupplierResponse>> GetAllAsync(CancellationToken ct)
        {
            var suppliers = await _db.Suppliers
                .AsNoTracking()
                .Select(s => new SupplierResponse { Id = s.Id, Name = s.Name })
                .ToListAsync(ct);

            // Сортировка в памяти: SQLite не умеет сравнивать кириллицу по культуре
            return suppliers
                .OrderBy(s => s.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        public async Task<SupplierResponse?> GetAsync(long id, CancellationToken ct)
        {
            return await _db.Suppliers
                .AsNoTracking()
                .Where(s => s.Id == id)
                .Select(s => new SupplierResponse { Id = s.Id, Name = s.Name })
                .FirstOrDefaultAsync(ct);
        }

        public async Task<SupplierResponse> EnsureAsync(string name, CancellationToken ct)
        {
            name = (name ?? "").Trim();

            if (name.Length == 0)
                throw new DomainException(Tr.T("Err_NeedSupplierName"));

            var existing = await FindByNameAsync(name, ct);

            if (existing is not null)
                return new SupplierResponse { Id = existing.Id, Name = existing.Name };

            var supplier = new Supplier(name);

            _db.Suppliers.Add(supplier);
            await _db.SaveChangesAsync(ct);

            return new SupplierResponse(supplier);
        }

        /// <summary>
        /// Поиск без учёта регистра. LIKE в SQLite игнорирует регистр только у латиницы,
        /// поэтому сверяем имена в памяти — справочник поставщиков заведомо небольшой.
        /// </summary>
        private async Task<Supplier?> FindByNameAsync(string name, CancellationToken ct)
        {
            var all = await _db.Suppliers.ToListAsync(ct);

            return all.FirstOrDefault(s =>
                string.Equals(s.Name, name, StringComparison.CurrentCultureIgnoreCase));
        }
    }
}
