using CureFlow.Application.Common;
using CureFlow.Application.DTOs;

namespace CureFlow.Application.Interfaces;

public interface ITaskService
{
    Task<Guid> CreateAsync(string title, Domain.Enums.TaskType type, Guid? patientId, string? notes, DateTime? dueAt, Domain.Enums.Priority priority, Guid? assignedTo = null, CancellationToken ct = default);
    Task<IReadOnlyList<object>> ListAsync(string? status, CancellationToken ct = default);
    Task UpdateAsync(Guid id, Domain.Enums.TaskStatus? status, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
