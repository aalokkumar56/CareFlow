using CureFlow.Application.Common;
using CureFlow.Application.DTOs;

namespace CureFlow.Application.Interfaces;

public interface ICampaignService
{
    Task<Guid> CreateAsync(string name, string? description, string messageBody, object audience, DateTime? scheduledAt = null, CancellationToken ct = default);
    Task<IReadOnlyList<object>> ListAsync(CancellationToken ct = default);
    Task<object> GetAsync(Guid id, CancellationToken ct = default);
    Task<(int count, IReadOnlyList<object> sample)> PreviewAudienceAsync(object audience, CancellationToken ct = default);
    Task<(int sent, int failed, int total)> SendAsync(Guid campaignId, CancellationToken ct = default);
    Task ScheduleAsync(Guid id, DateTime scheduledAtUtc, CancellationToken ct = default);
    Task UpdateAsync(Guid id, UpdateCampaignRequest update, CancellationToken ct = default);
    Task<IReadOnlyList<SuggestedCampaignDraftDto>> GetSuggestedDraftsAsync(CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
