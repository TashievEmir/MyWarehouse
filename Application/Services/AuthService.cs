using Application.Contracts.Interfaces;
using Application.Contracts.Persistence;
using Application.DTOs.Auths;
using Domain.Entities;
using Domain.Exceptions;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

using Application.Localization;

namespace Application.Services
{
    public class AuthService : IAuthService
    {
        private readonly IDataContext _db;
        private readonly IActivityLogService _activity;

        public AuthService(IDataContext db, IActivityLogService activity)
        {
            _db = db;
            _activity = activity;
        }

        public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken ct)
        {
            var user = await _db.Users
            .Include(x => x.Roles)
            .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(x => x.Username == request.Username, ct)
            ?? throw new DomainException(Tr.T("Err_InvalidCredentials"));

            if (!user.VerifyPassword(request.Password))
                throw new DomainException(Tr.T("Err_InvalidCredentials"));
            
            var result = new LoginResponse(user);

            await _activity.LogAsync(
                user.Id,
                Domain.Enums.ActivityType.LoggedIn,
                Tr.T("Log_LoggedIn"),
                result.Roles.Count > 0 ? Tr.F("Log_Role", string.Join(", ", result.Roles)) : null,
                "User",
                user.Id,
                ct);

            return result;
        }

        public async Task<long> RegisterAsync(RegisterRequest request, CancellationToken ct)
        {
            var exists = await _db.Users
        .AnyAsync(x => x.Username == request.Username, ct);

            if (exists)
                throw new DomainException(Tr.T("Err_UsernameTaken"));

            var user = new User(
                request.Username,
                request.Password,
                request.Lastname,
                request.Firstname,
                request.Patronymic,
                request.RoleIds);

            _db.Users.Add(user);
            await _db.SaveChangesAsync(ct);

            return user.Id;
        }
    }
}
