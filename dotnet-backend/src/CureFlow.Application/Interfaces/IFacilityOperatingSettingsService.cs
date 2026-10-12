using System.ComponentModel.DataAnnotations;
namespace CureFlow.Application.Interfaces;

public sealed record FacilityOperatingSettingsInput(
    [Required, StringLength(100)] string Timezone,
    [Required, StringLength(35)] string Language,
    [Required, StringLength(20)] string DateFormat,
    [Required, StringLength(20)] string TimeFormat,
    [Required, StringLength(500, MinimumLength = 3)] string Reason);
public sealed record FacilityOperatingSettingsView(Guid Id, Guid FacilityId, long Revision, DateTime EffectiveFrom,
    string Timezone, string Language, string DateFormat, string TimeFormat);
public interface IFacilityOperatingSettingsService
{
    Task<FacilityOperatingSettingsView?> GetAsync(Guid facilityId, CancellationToken ct);
    Task<IReadOnlyList<FacilityOperatingSettingsView>> HistoryAsync(Guid facilityId, long beforeRevision, int limit, CancellationToken ct);
    Task<IdempotentCommandResult> UpdateAsync(Guid facilityId, long expectedRevision, string key, FacilityOperatingSettingsInput input, CancellationToken ct);
}
