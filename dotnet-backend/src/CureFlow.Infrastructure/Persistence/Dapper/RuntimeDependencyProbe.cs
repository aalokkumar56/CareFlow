using Npgsql;

namespace CureFlow.Infrastructure.Persistence.Dapper;

/// <summary>Read-only dependency probing; never reads application rows or changes tenant scope.</summary>
public static class RuntimeDependencyProbe
{
    public static async Task CheckDatabaseAsync(NpgsqlDataSource source, string name, CancellationToken ct)
    {
        var authority = name switch
        {
            "Default" => DatabaseAuthority.Tenant,
            "Identity" => DatabaseAuthority.Identity,
            "Catalogue" => DatabaseAuthority.Catalogue,
            "PlatformIdentity" => DatabaseAuthority.PlatformIdentity,
            "ControlPlane" => DatabaseAuthority.Platform,
            _ => throw new ArgumentException("Unknown runtime dependency.", nameof(name))
        };
        await using var connection = await source.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await using (var readOnly = new NpgsqlCommand("SET TRANSACTION READ ONLY", connection, transaction))
            await readOnly.ExecuteNonQueryAsync(ct);
        await DatabaseRoleBoundary.ValidateAsync(connection, transaction, authority, ct);
        await using var probe = new NpgsqlCommand("SELECT 1", connection, transaction);
        if (await probe.ExecuteScalarAsync(ct) is not 1)
            throw new InvalidOperationException("Runtime dependency unavailable.");
        // Disposal rolls back the read-only transaction; no application state was written.
    }
}
