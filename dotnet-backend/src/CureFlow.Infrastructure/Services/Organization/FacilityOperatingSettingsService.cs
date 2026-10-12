using System.Globalization;
using CureFlow.Application.Common;
using CureFlow.Application.Interfaces;
using CureFlow.Domain.Entities;
using CureFlow.Domain.Entities.Saas;
using CureFlow.Domain.Entities.Organization;
using CureFlow.Infrastructure.Identity;
using Microsoft.Extensions.Configuration;

namespace CureFlow.Infrastructure.Services.Organization;

public sealed class FacilityOperatingSettingsService(ICureFlowDbSession db, ITenantContext tenant,
    IUserPermissionService permissions, IIdempotentCommandExecutor commands, IClinicalAccessAuditService audit,
    IConfiguration config) : IFacilityOperatingSettingsService
{
    private static readonly HashSet<string> Languages = CultureInfo.GetCultures(CultureTypes.AllCultures)
        .Where(x => x.Name.Length > 0).Select(x => x.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
    public static void Validate(FacilityOperatingSettingsInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Timezone) || input.Timezone.Length > 100 || input.Timezone != input.Timezone.Trim())
            throw new ValidationException("Select an IANA facility timezone.");
        try
        {
            var zone = TimeZoneInfo.FindSystemTimeZoneById(input.Timezone);
            if (!zone.HasIanaId) throw new ValidationException("Select an IANA facility timezone.");
        }
        catch (TimeZoneNotFoundException) { throw new ValidationException("Select a recognized IANA timezone."); }
        catch (InvalidTimeZoneException) { throw new ValidationException("Select a valid IANA timezone."); }
        if (string.IsNullOrWhiteSpace(input.Language) || input.Language.Length > 35 || !Languages.Contains(input.Language))
            throw new ValidationException("Select a recognized language tag.");
        try
        {
            var culture = CultureInfo.GetCultureInfo(input.Language);
            if (culture.Name.Length == 0) throw new ValidationException("Select a recognized language tag.");
        }
        catch (CultureNotFoundException) { throw new ValidationException("Select a recognized language tag."); }
        if (input.DateFormat is not ("yyyy-MM-dd" or "dd/MM/yyyy" or "MM/dd/yyyy") || input.TimeFormat is not ("HH:mm" or "h:mm tt"))
            throw new ValidationException("Select a supported date and time display format.");
        if (string.IsNullOrWhiteSpace(input.Reason) || input.Reason.Trim().Length < 3 || input.Reason.Length > 500)
            throw new ValidationException("Supply a bounded reason for this settings change.");
    }
    private async Task RequireAsync(ICureFlowDbSession store, Guid facilityId, string permission, bool writing, CancellationToken ct)
    {
        if (!tenant.IsAuthenticated || tenant.IsPlatformUser || tenant.TenantId == Guid.Empty || !tenant.UserId.HasValue)
            throw new DomainException("Current hospital authority is required.", 403);
        var user = await store.GetByIdAsync<User>(tenant.UserId.Value, ct: ct);
        if (user is not { IsActive: true } || !(await permissions.GetPermissionCodesAsync(user, ct, store)).Contains(permission, StringComparer.Ordinal))
            throw new DomainException("You do not have permission to manage facility settings.", 403);
        var facility = await store.QueryFirstOrDefaultAsync<Facility>("""
            SELECT * FROM "Facilities" WHERE "Id"=@facilityId AND "TenantId"=@TenantId AND NOT "IsDeleted"
            """, new { facilityId }, ct: ct) ?? throw new NotFoundException("Facility");
        if (writing && !facility.IsActive) throw new DomainException("Only an active facility can receive new operating settings.", 409);
    }
    private static FacilityOperatingSettingsView View(FacilityOperatingSetting row) =>
        new(row.Id,row.FacilityId,row.Revision,row.EffectiveFrom,row.Timezone,row.Language,row.DateFormat,row.TimeFormat);
    public async Task<FacilityOperatingSettingsView?> GetAsync(Guid facilityId, CancellationToken ct)
    {
        await RequireAsync(db,facilityId,CureFlowPermissions.SettingsView,false,ct);
        var row = await db.QueryFirstOrDefaultAsync<FacilityOperatingSetting>("""
            SELECT * FROM "FacilityOperatingSettings" WHERE "FacilityId"=@facilityId AND "TenantId"=@TenantId
            ORDER BY "Revision" DESC LIMIT 1
            """, new { facilityId }, ct:ct);
        await audit.RecordAsync(new(null,"Facility",facilityId,"read_operating_settings","authorized",row?.Revision.ToString(),"operations"),ct);
        return row is null ? null : View(row);
    }
    public async Task<IReadOnlyList<FacilityOperatingSettingsView>> HistoryAsync(Guid facilityId, long beforeRevision, int limit, CancellationToken ct)
    {
        await RequireAsync(db,facilityId,CureFlowPermissions.SettingsView,false,ct);
        var rows = await db.QueryAsync<FacilityOperatingSetting>("""
            SELECT * FROM "FacilityOperatingSettings" WHERE "FacilityId"=@facilityId AND "TenantId"=@TenantId
              AND (@beforeRevision=0 OR "Revision"<@beforeRevision) ORDER BY "Revision" DESC LIMIT @limit
            """,new{facilityId,beforeRevision=Math.Max(0,beforeRevision),limit=Math.Clamp(limit,1,100)},ct:ct);
        await audit.RecordAsync(new(null,"Facility",facilityId,"read_operating_settings_history","authorized",Purpose:"operations"),ct);
        return rows.Select(View).ToArray();
    }
    public Task<IdempotentCommandResult> UpdateAsync(Guid facilityId, long expectedRevision, string key, FacilityOperatingSettingsInput input, CancellationToken ct)
    {
        Validate(input);
        if(expectedRevision<0)throw new ValidationException("Supply the current settings version.");
        var horizon=config.GetValue<int?>("FacilityOperatingSettings:ApprovedRetryHorizonHours");
        if(horizon is not >0 or >8760)throw new DomainException("Facility settings command retry policy is not configured.",503);
        return commands.ExecuteAsync("facility.operating-settings",key,new{facilityId,expectedRevision,input},TimeSpan.FromHours(horizon.Value),
            (store,token)=>RequireAsync(store,facilityId,CureFlowPermissions.SettingsEdit,true,token),async(store,token)=>
            {
                // Serialize with the platform facility-change transaction; prevent deactivation racing the new version.
                await store.ExecuteAsync("SELECT pg_advisory_xact_lock(734291)",ct:token);
                await RequireAsync(store,facilityId,CureFlowPermissions.SettingsEdit,true,token);
                var prior=await store.QueryFirstOrDefaultAsync<FacilityOperatingSetting>("""
                    SELECT * FROM "FacilityOperatingSettings" WHERE "FacilityId"=@facilityId AND "TenantId"=@TenantId ORDER BY "Revision" DESC LIMIT 1
                    """,new{facilityId},ct:token);
                if((prior?.Revision??0)!=expectedRevision)throw new DomainException("Facility settings changed. Reload before saving.",412);
                var row=new FacilityOperatingSetting{FacilityId=facilityId,Revision=checked(expectedRevision+1),EffectiveFrom=DateTime.UtcNow,
                    Timezone=input.Timezone,Language=CultureInfo.GetCultureInfo(input.Language).Name,DateFormat=input.DateFormat,TimeFormat=input.TimeFormat,Reason=input.Reason.Trim()};
                await store.InsertAsync(row,ct:token);
                await audit.RecordAsync(new(null,"Facility",facilityId,"update_operating_settings","accepted",row.Revision.ToString(),"operations"),token,store);
                return(new CommandReceipt(row.Id,row.Revision,CommandOutcome.Updated),200);
            },ct);
    }
}
