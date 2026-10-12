using Npgsql;

namespace CureFlow.Infrastructure.Persistence.Dapper;

public static class CureFlowNpgsqlDataSource
{
    /// <summary>
    /// Hardens connection strings for cloud Postgres (Neon/Render) and Npgsql 10:
    /// disable GSS (missing libgssapi in .NET images), verify TLS off-localhost,
    /// and raise timeouts so cold-start DBs can wake before migrate fails.
    /// Remote connections cannot opt out of certificate and hostname validation.
    /// </summary>
    public static string Normalize(string connectionString)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString);

        if (!HasKey(connectionString, "GSS Encryption Mode")
            && !HasKey(connectionString, "GssEncryptionMode"))
        {
            builder.GssEncryptionMode = GssEncryptionMode.Disable;
        }

        var host = (builder.Host ?? string.Empty).Trim().ToLowerInvariant();
        var isLocal = host is "localhost" or "127.0.0.1" or "::1" or "postgres";
        if (!isLocal
            && !HasKey(connectionString, "SSL Mode")
            && !HasKey(connectionString, "Ssl Mode")
            && !HasKey(connectionString, "SslMode"))
        {
            builder.SslMode = SslMode.VerifyFull;
        }

        if (!isLocal && builder.SslMode != SslMode.VerifyFull)
            throw new InvalidOperationException("Remote PostgreSQL connections require SSL Mode=VerifyFull.");

        if (!HasKey(connectionString, "Timeout") && builder.Timeout < 60)
            builder.Timeout = 60;

        if (!HasKey(connectionString, "Command Timeout")
            && !HasKey(connectionString, "CommandTimeout")
            && builder.CommandTimeout < 60)
        {
            builder.CommandTimeout = 60;
        }

        return builder.ConnectionString;
    }

    public static NpgsqlDataSource Create(string connectionString)
    {
        var builder = new NpgsqlDataSourceBuilder(Normalize(connectionString));
        builder.AddTypeInfoResolverFactory(new LegacyDateAndTimeResolverFactory());
        return builder.Build();
    }

    private static bool HasKey(string connectionString, string key) =>
        connectionString.Contains(key, StringComparison.OrdinalIgnoreCase);
}
