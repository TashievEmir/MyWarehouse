using Application.Contracts.Interfaces;
using Application.Contracts.Persistence;
using Application.DTOs.Users;
using Application.Localization;
using Domain.Entities;
using Domain.Enums;
using Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Application.Services
{
    /// <summary>
    /// Сотрудники: список, заведение, правка и удаление.
    ///
    /// Правила, которые нельзя обойти из интерфейса:
    /// администратора заводит и правит только администратор, последнего админа
    /// нельзя разжаловать или отключить, себя нельзя удалить или выключить.
    /// </summary>
    public class UserService : IUserService
    {
        private const string Admin = "Admin";
        private const string Manager = "Manager";

        private readonly IDataContext _db;
        private readonly IActivityLogService _activity;

        public UserService(IDataContext db, IActivityLogService activity)
        {
            _db = db;
            _activity = activity;
        }

        public async Task<List<RoleResponse>> GetRolesAsync(CancellationToken ct)
        {
            var roles = await _db.Roles
                .AsNoTracking()
                .Select(r => new RoleResponse { Id = r.Id, Name = r.Name })
                .ToListAsync(ct);

            foreach (var role in roles)
                role.Title = RoleTitle(role.Name);

            return roles.OrderBy(r => r.Name, StringComparer.Ordinal).ToList();
        }

        public async Task<List<UserListItemResponse>> GetAllAsync(string? search, CancellationToken ct)
        {
            var rows = await _db.Users
                .AsNoTracking()
                .Select(u => new UserListItemResponse
                {
                    Id = u.Id,
                    Username = u.Username,
                    LastName = u.LastName,
                    FirstName = u.FirstName,
                    Patronymic = u.Patronymic,
                    IsActive = u.IsActive,
                    CreatedAt = u.CreatedAt,
                    RoleIds = u.Roles.Select(r => r.RoleId).ToList(),
                    RoleTitles = u.Roles.Select(r => r.Role.Name).ToList(),
                    HasHistory = _db.Sales.Any(s => s.UserId == u.Id)
                                 || _db.DebtPayments.Any(p => p.UserId == u.Id)
                                 || _db.StockWriteOffs.Any(w => w.UserId == u.Id),
                })
                .ToListAsync(ct);

            foreach (var row in rows)
                row.RoleTitles = row.RoleTitles.Select(RoleTitle).ToList();

            // Поиск в памяти: LIKE в SQLite игнорирует регистр только у латиницы
            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();

                rows = rows
                    .Where(r => Contains(r.Username, term)
                                || Contains(r.LastName, term)
                                || Contains(r.FirstName, term)
                                || Contains(r.Patronymic, term)
                                || r.RoleTitles.Any(t => Contains(t, term)))
                    .ToList();
            }

            return rows
                .OrderBy(r => r.LastName, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(r => r.FirstName, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        public async Task<long> SaveAsync(SaveUserRequest request, CancellationToken ct)
        {
            var actorRoles = await RolesOfAsync(request.ActorId, ct);

            if (!actorRoles.Any(r => r is Admin or Manager))
                throw new DomainException(Tr.T("Err_UsersNoRights"));

            var username = (request.Username ?? "").Trim();

            if (username.Length == 0)
                throw new DomainException(Tr.T("Err_NeedUsername"));

            if (request.RoleIds.Count == 0)
                throw new DomainException(Tr.T("Err_NeedRole"));

            var roleNames = await _db.Roles
                .Where(r => request.RoleIds.Contains(r.Id))
                .Select(r => r.Name)
                .ToListAsync(ct);

            if (roleNames.Count != request.RoleIds.Distinct().Count())
                throw new DomainException(Tr.T("Err_RoleNotFound"));

            // Менеджер не может создать администратора — иначе роль сама себя повышает
            if (roleNames.Contains(Admin) && !actorRoles.Contains(Admin))
                throw new DomainException(Tr.T("Err_OnlyAdminGrantsAdmin"));

            var taken = await _db.Users
                .AnyAsync(u => u.Username == username && u.Id != request.Id, ct);

            if (taken)
                throw new DomainException(Tr.T("Err_UsernameTaken"));

            return request.Id > 0
                ? await UpdateAsync(request, username, roleNames, actorRoles, ct)
                : await CreateAsync(request, username, ct);
        }

        private async Task<long> CreateAsync(SaveUserRequest request, string username, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(request.Password))
                throw new DomainException(Tr.T("Err_NeedPassword"));

            var user = new User(
                username,
                request.Password,
                request.LastName,
                request.FirstName,
                request.Patronymic,
                request.RoleIds);

            user.SetActive(request.IsActive);

            _db.Users.Add(user);
            await _db.SaveChangesAsync(ct);

            await _activity.LogAsync(
                request.ActorId,
                ActivityType.UserCreated,
                Tr.T("Log_UserCreated"),
                Tr.F("Log_UserDetails", $"{user.LastName} {user.FirstName}".Trim(), user.Username),
                "User",
                user.Id,
                ct);

            return user.Id;
        }

        private async Task<long> UpdateAsync(
            SaveUserRequest request,
            string username,
            List<string> newRoleNames,
            List<string> actorRoles,
            CancellationToken ct)
        {
            var user = await _db.Users
                .Include(u => u.Roles)
                .ThenInclude(r => r.Role)
                .FirstOrDefaultAsync(u => u.Id == request.Id, ct)
                ?? throw new DomainException(Tr.T("Err_UserNotFound"));

            var currentRoles = user.Roles.Select(r => r.Role.Name).ToList();

            // Чужую админскую учётку менеджер не трогает
            if (currentRoles.Contains(Admin) && !actorRoles.Contains(Admin))
                throw new DomainException(Tr.T("Err_OnlyAdminEditsAdmin"));

            // Последний админ должен остаться админом и остаться включённым
            var losesAdmin = currentRoles.Contains(Admin) && !newRoleNames.Contains(Admin);

            if ((losesAdmin || !request.IsActive) && currentRoles.Contains(Admin) && await IsLastActiveAdminAsync(user.Id, ct))
                throw new DomainException(Tr.T("Err_LastAdmin"));

            if (!request.IsActive && user.Id == request.ActorId)
                throw new DomainException(Tr.T("Err_CannotDisableSelf"));

            user.Update(username, request.LastName, request.FirstName, request.Patronymic);
            user.ReplaceRoles(request.RoleIds);
            user.SetActive(request.IsActive);

            // Пустой пароль при правке — оставляем прежний
            if (!string.IsNullOrWhiteSpace(request.Password))
                user.SetPassword(request.Password);

            await _db.SaveChangesAsync(ct);

            await _activity.LogAsync(
                request.ActorId,
                ActivityType.UserUpdated,
                Tr.T("Log_UserUpdated"),
                Tr.F("Log_UserDetails", $"{user.LastName} {user.FirstName}".Trim(), user.Username),
                "User",
                user.Id,
                ct);

            return user.Id;
        }

        public async Task DeleteAsync(long userId, long actorId, CancellationToken ct)
        {
            var actorRoles = await RolesOfAsync(actorId, ct);

            if (!actorRoles.Any(r => r is Admin or Manager))
                throw new DomainException(Tr.T("Err_UsersNoRights"));

            if (userId == actorId)
                throw new DomainException(Tr.T("Err_CannotDeleteSelf"));

            var user = await _db.Users
                .Include(u => u.Roles)
                .ThenInclude(r => r.Role)
                .FirstOrDefaultAsync(u => u.Id == userId, ct)
                ?? throw new DomainException(Tr.T("Err_UserNotFound"));

            var roles = user.Roles.Select(r => r.Role.Name).ToList();

            if (roles.Contains(Admin) && !actorRoles.Contains(Admin))
                throw new DomainException(Tr.T("Err_OnlyAdminEditsAdmin"));

            if (roles.Contains(Admin) && await IsLastActiveAdminAsync(user.Id, ct))
                throw new DomainException(Tr.T("Err_LastAdmin"));

            var hasHistory = await _db.Sales.AnyAsync(s => s.UserId == userId, ct)
                             || await _db.DebtPayments.AnyAsync(p => p.UserId == userId, ct)
                             || await _db.StockWriteOffs.AnyAsync(w => w.UserId == userId, ct);

            if (hasHistory)
                throw new DomainException(Tr.T("Err_UserHasHistory"));

            var name = $"{user.LastName} {user.FirstName}".Trim();

            _db.Users.Remove(user);
            await _db.SaveChangesAsync(ct);

            await _activity.LogAsync(
                actorId,
                ActivityType.UserDeleted,
                Tr.T("Log_UserDeleted"),
                Tr.F("Log_UserDetails", name, user.Username),
                "User",
                null,
                ct);
        }

        // ===================== Вспомогательное =====================

        private async Task<List<string>> RolesOfAsync(long userId, CancellationToken ct)
        {
            if (userId <= 0)
                throw new DomainException(Tr.T("Err_NoEmployee"));

            return await _db.Users
                .Where(u => u.Id == userId)
                .SelectMany(u => u.Roles.Select(r => r.Role.Name))
                .ToListAsync(ct);
        }

        private async Task<bool> IsLastActiveAdminAsync(long exceptUserId, CancellationToken ct)
        {
            var others = await _db.Users
                .Where(u => u.Id != exceptUserId && u.IsActive)
                .CountAsync(u => u.Roles.Any(r => r.Role.Name == Admin), ct);

            return others == 0;
        }

        private static bool Contains(string? value, string term)
            => value is not null && value.Contains(term, StringComparison.CurrentCultureIgnoreCase);

        private static string RoleTitle(string name) => name switch
        {
            Admin   => Tr.T("Role_Admin"),
            Manager => Tr.T("Role_Manager"),
            "Cashier" => Tr.T("Role_Cashier"),
            _ => name,
        };
    }
}
