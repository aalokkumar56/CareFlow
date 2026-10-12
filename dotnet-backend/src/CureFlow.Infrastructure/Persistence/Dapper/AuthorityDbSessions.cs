using CureFlow.Application.Common;
using CureFlow.Application.Interfaces;
using Npgsql;

namespace CureFlow.Infrastructure.Persistence.Dapper;

internal enum DatabaseAuthority { Tenant, Identity, Catalogue, PlatformIdentity, Platform, Provisioning }

public sealed class IdentityDbSession(NpgsqlDataSource source, ITenantContext tenant)
    : CureFlowDbSession(source, tenant, DatabaseAuthority.Identity), IIdentityDbSession;
public sealed class CatalogueDbSession(NpgsqlDataSource source, ITenantContext tenant)
    : CureFlowDbSession(source, tenant, DatabaseAuthority.Catalogue), ICatalogueDbSession;
public sealed class PlatformIdentityDbSession(NpgsqlDataSource source, ITenantContext tenant)
    : CureFlowDbSession(source, tenant, DatabaseAuthority.PlatformIdentity), IPlatformIdentityDbSession;
public sealed class PlatformDbSession(NpgsqlDataSource source, ITenantContext tenant)
    : CureFlowDbSession(source, tenant, DatabaseAuthority.Platform), IPlatformDbSession;
public sealed class ProvisioningDbSession(NpgsqlDataSource source, ITenantContext tenant)
    : CureFlowDbSession(source, tenant, DatabaseAuthority.Provisioning), IProvisioningDbSession;

internal static class DatabaseRoleBoundary
{
    public static async Task ValidateAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, DatabaseAuthority authority, CancellationToken ct)
    {
        var group = authority switch
        {
            DatabaseAuthority.Tenant => "cureflow_tenant",
            DatabaseAuthority.Identity => "cureflow_identity",
            DatabaseAuthority.Catalogue => "cureflow_catalogue",
            DatabaseAuthority.PlatformIdentity => "cureflow_platform_identity",
            _ => "cureflow_control_plane"
        };
        await using var command = new NpgsqlCommand("""
            SELECT NOT r.rolsuper AND NOT r.rolbypassrls
                AND EXISTS (SELECT 1 FROM pg_roles g WHERE g.rolname=@group AND pg_has_role(current_user,g.oid,'MEMBER'))
                AND NOT EXISTS (SELECT 1 FROM pg_roles other WHERE other.rolname<>@group
                    AND other.rolname=ANY(ARRAY['cureflow_tenant','cureflow_identity','cureflow_catalogue','cureflow_control_plane','cureflow_platform_identity'])
                    AND pg_has_role(current_user,other.oid,'MEMBER'))
                AND NOT EXISTS (SELECT 1 FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace
                    WHERE n.nspname='public' AND c.relkind IN ('r','p') AND pg_has_role(current_user,c.relowner,'MEMBER'))
                AND (@tenant OR NOT EXISTS (SELECT 1 FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace
                    WHERE n.nspname='public' AND c.relkind IN ('r','p') AND NOT (c.relname=ANY(@allowed))
                      AND (has_table_privilege(current_user,c.oid,'SELECT') OR has_table_privilege(current_user,c.oid,'INSERT')
                        OR has_table_privilege(current_user,c.oid,'UPDATE') OR has_table_privilege(current_user,c.oid,'DELETE')
                        OR has_any_column_privilege(current_user,c.oid,'SELECT')
                        OR has_any_column_privilege(current_user,c.oid,'INSERT')
                        OR has_any_column_privilege(current_user,c.oid,'UPDATE'))))
                AND (NOT @platform OR NOT EXISTS (
                    SELECT 1 FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace
                    WHERE n.nspname='public' AND c.relname='PlatformSessions'
                      AND (has_table_privilege(current_user,c.oid,'SELECT,INSERT,UPDATE,DELETE,TRUNCATE,REFERENCES,TRIGGER')
                        OR EXISTS (SELECT 1 FROM pg_attribute a WHERE a.attrelid=c.oid AND a.attnum>0 AND NOT a.attisdropped
                          AND ((has_column_privilege(current_user,c.oid,a.attnum,'SELECT')
                              AND NOT (a.attname=ANY(ARRAY['Id','PlatformUserId','ExpiresAt','RevokedAt','SecurityRevision','IsActive','IsDeleted'])))
                            OR (has_column_privilege(current_user,c.oid,a.attnum,'UPDATE')
                              AND NOT (a.attname=ANY(ARRAY['RevokedAt','UpdatedAt','SecurityRevision'])))
                            OR has_column_privilege(current_user,c.oid,a.attnum,'INSERT')
                            OR has_column_privilege(current_user,c.oid,a.attnum,'REFERENCES'))))))
                AND NOT EXISTS (SELECT 1 FROM pg_proc p JOIN pg_namespace n ON n.oid=p.pronamespace
                    WHERE n.nspname='public' AND p.prosecdef AND has_function_privilege(current_user,p.oid,'EXECUTE')
                      AND NOT (p.proname=ANY(@allowedFunctions)))
            FROM pg_roles r WHERE r.rolname=current_user
            """, connection, transaction);
        command.Parameters.AddWithValue("group", group);
        command.Parameters.AddWithValue("tenant", authority == DatabaseAuthority.Tenant);
        command.Parameters.AddWithValue("platform", authority == DatabaseAuthority.Platform);
        var allowed = authority switch
        {
            DatabaseAuthority.Catalogue => new[] { "Tenants" },
            DatabaseAuthority.PlatformIdentity => new[] { "PlatformUsers", "PlatformSessions", "PlatformAuditLogs" },
            DatabaseAuthority.Identity => new[] { "Users", "UserRoleAssignments", "Tenants", "HospitalProfiles", "TenantOnboardingStates", "TemplatePlaceholders", "RefreshSessions", "Roles", "Permissions", "RolePermissions", "PlatformModules", "PlanModules" },
            _ => new[] { "Tenants", "PlatformUsers", "PlatformRoles", "PlatformSessions", "PlatformCommands", "PlatformModules", "PlanModules", "TenantEntitlements", "Facilities", "PlatformOperationJobs", "PlatformSupportSessions", "TenantOnboardingStates", "PlatformAuditLogs", "TenantApiActivities" }
        };
        command.Parameters.AddWithValue("allowed", allowed);
        command.Parameters.AddWithValue("allowedFunctions", authority switch
        {
            DatabaseAuthority.Tenant => new[] { "cureflow_patient_read_allowed", "cureflow_lock_active_facility" },
            DatabaseAuthority.Catalogue => new[] { "cureflow_webhook_tenant" },
            DatabaseAuthority.Platform => new[] { "cureflow_platform_usage" },
            _ => Array.Empty<string>()
        });
        if (await command.ExecuteScalarAsync(ct) is not true)
            throw new InvalidOperationException("Database runtime role does not satisfy its authority boundary. Deploy the reviewed restricted role grants.");
    }
}
