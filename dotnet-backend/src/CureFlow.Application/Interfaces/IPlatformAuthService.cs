using CureFlow.Application.DTOs;

namespace CureFlow.Application.Interfaces;

public interface IPlatformAuthService
{
    Task<PlatformSetupStatusResponse> GetSetupStatusAsync(CancellationToken ct = default);
    Task<PlatformAuthResponse> BootstrapOwnerAsync(PlatformBootstrapRequest req, CancellationToken ct = default);
    Task<PlatformAuthResponse> LoginAsync(LoginRequest req, CancellationToken ct = default);
}
