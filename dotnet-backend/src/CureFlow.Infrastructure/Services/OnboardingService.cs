using CureFlow.Application.Common;
using CureFlow.Application.DTOs;
using CureFlow.Application.Interfaces;
using CureFlow.Application.Options;
using CureFlow.Domain.Entities;
using CureFlow.Domain.Entities.Saas;
using CureFlow.Domain.Enums;
using Microsoft.Extensions.Options;
namespace CureFlow.Infrastructure.Services;

public sealed class OnboardingService(ICureFlowDbSession db, ITenantContext context,
    IOptions<OrganizationOnboardingOptions> options, IClinicalAccessAuditService audit) : IOnboardingService
{
    private Task<Tenant> HospitalAsync(ICureFlowDbSession store, bool locked, CancellationToken ct) =>
        LoadHospitalAsync(store, locked, ct);
    private async Task<Tenant> LoadHospitalAsync(ICureFlowDbSession store, bool locked, CancellationToken ct)
    {
        if (!context.IsAuthenticated || context.IsPlatformUser || context.TenantId == Guid.Empty || !context.UserId.HasValue)
            throw new DomainException("A current hospital user is required.", 403);
        return await store.QueryFirstOrDefaultAsync<Tenant>(
            """SELECT * FROM "Tenants" WHERE "Id"=@TenantId AND NOT "IsDeleted" """ + (locked ? " FOR UPDATE" : ""),
            ct: ct) ?? throw new NotFoundException("Hospital");
    }
    private static Task<TenantOnboardingState?> StateAsync(ICureFlowDbSession store, CancellationToken ct) =>
        store.QueryFirstOrDefaultAsync<TenantOnboardingState>("""
            SELECT * FROM "TenantOnboardingStates" WHERE "TenantId"=@TenantId AND NOT "IsDeleted"
            """, ct: ct);
    public async Task<OnboardingStateDto> GetStateAsync(CancellationToken ct = default)
    {
        var hospital = await HospitalAsync(db, false, ct);
        return Map(await StateAsync(db, ct) ?? new TenantOnboardingState(), hospital);
    }
    public Task<OnboardingStateDto> MarkProfileCompleteAsync(CancellationToken ct = default) => MutateAsync("profile", ct);
    public Task<OnboardingStateDto> MarkTeamInvitedAsync(CancellationToken ct = default) => MutateAsync("team", ct);
    public Task<OnboardingStateDto> MarkWhatsAppConnectedAsync(CancellationToken ct = default) =>
        throw new DomainException("Verified provider connection evidence is unavailable. No connection is inferred from saved credentials.", 409);
    public Task<OnboardingStateDto> CompleteAsync(CancellationToken ct = default) => MutateAsync("complete", ct);

    private async Task<OnboardingStateDto> MutateAsync(string step, CancellationToken ct)
    {
        if (context is not CurrentTenant current || !current.CurrentPermissions.Contains(CureFlowPermissions.SettingsEdit, StringComparer.Ordinal))
            throw new DomainException("You do not have permission to change hospital setup.", 403);
        OnboardingStateDto? result = null;
        await db.TransactionAsync(async store =>
        {
            var hospital = await HospitalAsync(store, true, ct);
            if (hospital.LifecycleStatus is TenantLifecycleStatus.Rejected or TenantLifecycleStatus.Suspended)
                throw new DomainException("Hospital setup is unavailable in this lifecycle state.", 403);
            var state = await StateAsync(store, ct);
            var create = state is null;
            state ??= new TenantOnboardingState();
            if (step == "profile")
            {
                var profile = await store.QueryFirstOrDefaultAsync<HospitalProfile>(
                    """SELECT * FROM "HospitalProfiles" WHERE "TenantId"=@TenantId AND "IsActive" AND NOT "IsDeleted" """, ct: ct);
                if (profile is null || string.IsNullOrWhiteSpace(profile.Name))
                    throw new DomainException("Save a valid hospital profile before completing this step.", 409);
                state.ProfileComplete = true;
            }
            if (step == "team")
            {
                var activeColleague = await store.QueryFirstOrDefaultAsync<Guid?>("""
                    SELECT "Id" FROM "Users" WHERE "TenantId"=@TenantId AND "Id"<>@actor AND "IsActive" AND NOT "IsDeleted" LIMIT 1
                    """, new { actor = context.UserId }, ct: ct);
                if (!activeColleague.HasValue) throw new DomainException("Create an active team member before completing this step.", 409);
                state.TeamInvited = true;
            }
            if (step == "complete")
            {
                if (hospital.LifecycleStatus != TenantLifecycleStatus.Active || !hospital.IsActive
                    || !hospital.ApprovedAt.HasValue || !hospital.ApprovedByPlatformUserId.HasValue)
                    throw new DomainException("Hospital approval is required before completing onboarding.", 409);
                if (!hospital.OnboardingComplete)
                {
                    var settings = options.Value.For(hospital.TenantType);
                    if (settings.RequireWhatsApp)
                        throw new DomainException("Verified provider connection evidence is unavailable for required onboarding.", 409);
                    if (settings.RequireProfile)
                    {
                        var validProfile = await store.QueryFirstOrDefaultAsync<Guid?>(
                            """SELECT "Id" FROM "HospitalProfiles" WHERE "TenantId"=@TenantId AND "IsActive" AND NOT "IsDeleted" AND length(trim("Name"))>0 FOR SHARE""", ct: ct);
                        if (!validProfile.HasValue) throw new DomainException("A current valid hospital profile is required.", 409);
                    }
                    if (settings.RequireTeam)
                    {
                        var activeTeam = await store.QueryFirstOrDefaultAsync<Guid?>(
                            """SELECT "Id" FROM "Users" WHERE "TenantId"=@TenantId AND "Id"<>@actor AND "IsActive" AND NOT "IsDeleted" LIMIT 1 FOR SHARE""",
                            new { actor = context.UserId }, ct: ct);
                        if (!activeTeam.HasValue) throw new DomainException("A current active team member is required.", 409);
                    }
                    if ((settings.RequireProfile && !state.ProfileComplete) || (settings.RequireTeam && !state.TeamInvited)
                        || (settings.RequireWhatsApp && !state.WhatsAppConnected))
                        throw new DomainException("Complete the configured required onboarding steps first.", 409);
                    state.CompletedAt ??= DateTime.UtcNow;
                    // Never rewrite plan, lifecycle, approval, type, entitlements or clinical associations.
                    await store.ExecuteAsync("""
                        UPDATE "Tenants" SET "OnboardingComplete"=true,"UpdatedAt"=CURRENT_TIMESTAMP WHERE "Id"=@TenantId
                        """, ct: ct);
                    hospital.OnboardingComplete = true;
                }
            }
            if (create) await store.InsertAsync(state, ct: ct); else await store.UpdateAsync(state, ct: ct);
            await audit.RecordAsync(new(null, "TenantOnboardingState", state.Id, "onboarding_" + step, "committed", Purpose: "operations"), ct, store);
            result = Map(state, hospital);
        }, ct);
        return result!;
    }
    private OnboardingStateDto Map(TenantOnboardingState state, Tenant hospital)
    {
        var defaults = options.Value.For(hospital.TenantType);
        var required = new List<string>();
        if (defaults.RequireProfile) required.Add("profile");
        if (defaults.RequireTeam) required.Add("team");
        if (defaults.RequireWhatsApp) required.Add("whatsapp");
        return new(state.ProfileComplete, state.WhatsAppConnected, state.TeamInvited, hospital.OnboardingComplete, required);
    }
}


