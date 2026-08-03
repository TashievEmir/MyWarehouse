using Application.DTOs.Suppliers;

namespace Application.Contracts.Interfaces
{
    public interface ISupplierService
    {
        Task<List<SupplierResponse>> GetAllAsync(CancellationToken ct);

        Task<SupplierResponse?> GetAsync(long id, CancellationToken ct);

        /// <summary>
        /// Заводит поставщика или возвращает уже существующего с таким же именем.
        /// Приёмка вызывает это и вручную, и при сохранении прихода.
        /// </summary>
        Task<SupplierResponse> EnsureAsync(string name, CancellationToken ct);
    }
}
