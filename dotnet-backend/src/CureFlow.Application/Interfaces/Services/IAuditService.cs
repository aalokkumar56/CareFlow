using CureFlow.Application.DTOs;
using CureFlow.Application.Common;
using CureFlow.Domain.Enums;

namespace CureFlow.Application.Interfaces;

public interface IAuditService
{
    Task LogAsync(string action, string? entityType = null, string? entityId = null, object? metadata = null, CancellationToken ct = default);
    Task<IReadOnlyList<object>> ListAsync(int limit, CancellationToken ct = default);
}
