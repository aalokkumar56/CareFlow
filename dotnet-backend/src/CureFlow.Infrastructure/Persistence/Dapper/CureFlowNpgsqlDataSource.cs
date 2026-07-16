using Npgsql;

namespace CureFlow.Infrastructure.Persistence.Dapper;

public static class CureFlowNpgsqlDataSource
{
    /// <summary>
    /// Hardens connection strings for cloud Postgres (Neon/Render) and Npgsql 10:
    /// disable GSS (missing libgssapi in .NET images), require SSL off-localhost,
    /// and raise timeouts so cold-start DBs can wake before migrate fails.
    /// Explicit connection-string values are left unchanged.
    /// </summary>
    public static string Normalize(string connectionString)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString);

        if (!HasKey(connectionString, "GSS Encryption Mode")
            && !HasKey(connectionString, "GssEncryptionMode"))
        {
            builder.GssEncryptionMode = GssEncryptionMode.Disable;
        }

        var host = builder.Host ?? string.Empty;
        var isLocal = host is "localhost" or "127.0.0.1" or "::1" or "postgres";
        if (!isLocal
            && !HasKey(connectionString, "SSL Mode")
            && !HasKey(connectionString, "Ssl Mode")
            && !HasKey(connectionString, "SslMode"))
        {
            builder.SslMode = SslMode.Require;
        }

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
