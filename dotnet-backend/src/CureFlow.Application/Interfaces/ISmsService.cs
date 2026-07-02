namespace CureFlow.Application.Interfaces;

public interface ISmsService
{
    Task<IntegrationStatusDto> GetStatusAsync(CancellationToken ct = default);
    Task<(bool ok, string? error)> SendAsync(string toPhone, string body, CancellationToken ct = default);
}
