using CureFlow.Domain.Entities;
namespace CureFlow.Application.Interfaces;

public record ScheduleExceptionInput(Guid? PractitionerUserId, Guid? ResourceId, string Kind,
    DateTime StartsAtUtc, DateTime EndsAtUtc, string Timezone, string Reason);
public interface IScheduleExceptionService
{
    Task<IReadOnlyList<ScheduleException>> ListAsync(Guid facilityId, Guid? afterId, int limit, CancellationToken ct);
    Task<ScheduleException> GetAsync(Guid facilityId, Guid exceptionId, CancellationToken ct);
    Task<IReadOnlyList<AppointmentRescheduleWork>> WorkAsync(Guid facilityId, Guid? afterId, int limit, CancellationToken ct);
    Task<IdempotentCommandResult> CreateAsync(Guid facilityId, string key, ScheduleExceptionInput input, CancellationToken ct);
    Task<IdempotentCommandResult> DisableAsync(Guid facilityId, Guid exceptionId, long expectedRevision, string key, CancellationToken ct);
}
