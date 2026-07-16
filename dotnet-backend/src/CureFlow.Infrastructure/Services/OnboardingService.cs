using CureFlow.Application.Common;
using CureFlow.Application.DTOs;
using CureFlow.Application.Interfaces;
using CureFlow.Domain.Entities.Saas;

namespace CureFlow.Infrastructure.Services;

public class OnboardingService : IOnboardingService
{
    private readonly ICureFlowDbSession _db;
    private readonly ITenantContext _tenant;

    public OnboardingService(ICureFlowDbSession db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<OnboardingStateDto> GetStateAsync(CancellationToken ct = default)
    {
        var state = await GetOrCreateStateAsync(ct);
        return Map(state);
    }

    public async Task<OnboardingStateDto> MarkProfileCompleteAsync(CancellationToken ct = default)
    {
        var state = await GetOrCreateStateAsync(ct);
        state.ProfileComplete = true;
        await _db.UpdateAsync(state, ct: ct);
        return Map(state);
    }

    public async Task<OnboardingStateDto> MarkWhatsAppConnectedAsync(CancellationToken ct = default)
    {
        var state = await GetOrCreateStateAsync(ct);
        state.WhatsAppConnected = true;
        await _db.UpdateAsync(state, ct: ct);
        return Map(state);
    }

    public async Task<OnboardingStateDto> MarkTeamInvitedAsync(CancellationToken ct = default)
    {
        var state = await GetOrCreateStateAsync(ct);
        state.TeamInvited = true;
        await _db.UpdateAsync(state, ct: ct);
        return Map(state);
    }

    public async Task<OnboardingStateDto> CompleteAsync(CancellationToken ct = default)
    {
        var state = await GetOrCreateStateAsync(ct);
        state.ProfileComplete = true;
        state.WhatsAppConnected = true;
        state.TeamInvited = true;
        state.CompletedAt = DateTime.UtcNow;
        await _db.UpdateAsync(state, ct: ct);

        var tenant = await _db.QueryFirstOrDefaultAsync<Tenant>(
            """SELECT * FROM "Tenants" WHERE "Id" = @Id LIMIT 1""",
            new { Id = _tenant.TenantId },
            ignoreTenant: true,
            ct);
        if (tenant != null)
        {
            tenant.OnboardingComplete = true;
            await _db.UpdateAsync(tenant, ignoreTenant: true, ct);
        }

        return Map(state);
    }

    private async Task<TenantOnboardingState> GetOrCreateStateAsync(CancellationToken ct)
    {
        var state = await _db.QueryFirstOrDefaultAsync<TenantOnboardingState>(
            """
            SELECT * FROM "TenantOnboardingStates"
            WHERE "TenantId" = @TenantId AND "IsDeleted" = false
            LIMIT 1
            """,
            new { TenantId = _tenant.TenantId },
            ignoreTenant: true,
            ct);

        if (state != null) return state;

        state = new TenantOnboardingState { TenantId = _tenant.TenantId };
        await _db.InsertAsync(state, ignoreTenant: true, ct);
        return state;
    }

    private static OnboardingStateDto Map(TenantOnboardingState state) =>
        new(
            state.ProfileComplete,
            state.WhatsAppConnected,
            state.TeamInvited,
            state.ProfileComplete && state.WhatsAppConnected && state.TeamInvited);
}
