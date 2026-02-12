using Application.DTOs.Auths;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Contracts.Interfaces
{
    public interface IAuthService
    {
        Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken ct);
        Task<long> RegisterAsync(RegisterRequest request, CancellationToken ct);
    }
}
