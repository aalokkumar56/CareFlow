using CureFlow.Application.DTOs;
using CureFlow.Application.Common;
using CureFlow.Domain.Enums;

namespace CureFlow.Application.Interfaces;

public interface IAuthService
{
    Task<AuthResponse> LoginAsync(LoginRequest req, CancellationToken ct = default);
    Task<TenantDto> RegisterTenantAsync(RegisterTenantRequest req, CancellationToken ct = default);
    Task<UserDto> CreateUserAsync(CreateUserRequest req, CancellationToken ct = default);
    Task<UserDto> GetMeAsync(CancellationToken ct = default);
    Task<SessionDto> GetSessionAsync(CancellationToken ct = default);
}
