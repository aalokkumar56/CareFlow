using CureFlow.Application.DTOs;

namespace CureFlow.Application.Interfaces;

public interface IPlatformAuthService
{
    Task<PlatformSessionResponse> GetSessionAsync(CancellationToken ct = default);
    Task LogoutAsync(CancellationToken ct = default);
    Task<PlatformSetupStatusResponse> GetSetupStatusAsync(CancellationToken ct = default);
    Task<PlatformAuthResponse> BootstrapOwnerAsync(PlatformBootstrapRequest req, CancellationToken ct = default);
    Task<PlatformAuthResponse> LoginAsync(LoginRequest req, CancellationToken ct = default);
}
