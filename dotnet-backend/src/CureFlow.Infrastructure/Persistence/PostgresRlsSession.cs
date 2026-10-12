using Npgsql;

namespace CureFlow.Infrastructure.Persistence;

/// <summary>
/// Sets PostgreSQL session variables consumed by row-level security policies.
/// </summary>
public static class PostgresRlsSession
{
    public const string TenantSetting = "app.current_tenant_id";
    public const string PlatformSetting = "app.platform_admin";

    /// <summary>
    /// Clears then applies tenant scope or platform bypass before tenant-scoped SQL runs.
    /// Resets first so pooled connections do not leak prior request context.
    /// </summary>
    public static async Task ConfigureAsync(NpgsqlConnection connection, Guid tenantId, bool platformBypass, CancellationToken ct = default, NpgsqlTransaction? transaction = null, Guid? actorId = null)
    {
        if (transaction is null) throw new InvalidOperationException("RLS scope requires an active transaction.");
        await using (var reset = new NpgsqlCommand(
                         """
                         SELECT set_config('app.current_tenant_id', '', true),
                                set_config('app.platform_admin', '', true),
                                set_config('app.current_user_id', '', true)
                         """,
                         connection, transaction))
            await reset.ExecuteNonQueryAsync(ct);

        if (tenantId != Guid.Empty)
        {
            await using var cmd = new NpgsqlCommand(
                $"SELECT set_config('{TenantSetting}', @tid, true), set_config('app.current_user_id', @actor, true)",
                connection, transaction);
            cmd.Parameters.AddWithValue("tid", tenantId.ToString());
            cmd.Parameters.AddWithValue("actor", actorId is { } id && id != Guid.Empty ? id.ToString("D") : string.Empty);
            await cmd.ExecuteNonQueryAsync(ct);
            return;
        }

        if (platformBypass)
        {
            await using var cmd = new NpgsqlCommand(
                $"SELECT set_config('{PlatformSetting}', 'true', true)",
                connection, transaction);
            await cmd.ExecuteNonQueryAsync(ct);
        }
    }

    /// <summary>Returns true when the DB role bypasses RLS (e.g. postgres superuser).</summary>
    public static async Task<bool> CurrentRoleBypassesRlsAsync(NpgsqlConnection connection, CancellationToken ct = default)
    {
        await using var cmd = new NpgsqlCommand(
            "SELECT rolsuper OR rolbypassrls FROM pg_roles WHERE rolname = current_user",
            connection);
        var result = await cmd.ExecuteScalarAsync(ct);
        return result is bool bypass && bypass;
    }
}
