using Application.DTOs.Users;

namespace Application.Contracts.Interfaces
{
    public interface IUserService
    {
        Task<List<RoleResponse>> GetRolesAsync(CancellationToken ct);

        /// <summary>Список сотрудников. Пустой поиск — все.</summary>
        Task<List<UserListItemResponse>> GetAllAsync(string? search, CancellationToken ct);

        /// <summary>Создаёт сотрудника или правит существующего. Возвращает Id.</summary>
        Task<long> SaveAsync(SaveUserRequest request, CancellationToken ct);

        /// <summary>
        /// Удаляет сотрудника. Того, за кем числятся продажи, платежи или списания,
        /// удалить нельзя — вместе с ним пропала бы история. Такого отключают.
        /// </summary>
        Task DeleteAsync(long userId, long actorId, CancellationToken ct);
    }
}
