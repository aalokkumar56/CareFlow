namespace CureFlow.Application.Interfaces;

public interface IExternalProcessingPolicy
{
    Task<bool> IsAllowedAsync(string provider, string purpose, CancellationToken ct);
    Task RequireDispatchAsync(string provider, string purpose, string resourceType, Guid? resourceId,
        string? resourceVersion, CancellationToken ct);
}
