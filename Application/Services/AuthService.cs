using Application.Contracts.Interfaces;
using Application.Contracts.Persistence;
using Application.DTOs.Auths;
using Domain.Entities;
using Domain.Exceptions;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Services
{
    public class AuthService : IAuthService
    {
        private readonly IDataContext _db;

        public AuthService(IDataContext db)
        {
            _db = db;
        }

        public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken ct)
        {
            var user = await _db.Users
            .Include(x => x.Roles)
            .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(x => x.Username == request.Username, ct)
            ?? throw new DomainException("Invalid credentials");

            if (!user.VerifyPassword(request.Password))
                throw new DomainException("Invalid credentials");
            
            var result = new LoginResponse(user);
            return result;
        }

        public async Task<long> RegisterAsync(RegisterRequest request, CancellationToken ct)
        {
            var exists = await _db.Users
        .AnyAsync(x => x.Username == request.Username, ct);

            if (exists)
                throw new DomainException("Username already exists");

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
