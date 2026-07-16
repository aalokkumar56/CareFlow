using CureFlow.Application.DTOs;

namespace CureFlow.Application.Interfaces;

public interface IPlatformAuthService
{
    Task<PlatformAuthResponse> LoginAsync(LoginRequest req, CancellationToken ct = default);
}
