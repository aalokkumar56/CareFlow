using Npgsql;

namespace CureFlow.Infrastructure.Persistence.Dapper;

public static class CureFlowNpgsqlDataSource
{
    /// <summary>
    /// Npgsql 10 defaults to Prefer GSS encryption. Official .NET container images
    /// omit libgssapi_krb5, which logs a noisy (harmless) stderr error on first connect.
    /// Disable GSS unless the connection string already sets an explicit mode.
    /// </summary>
    public static string Normalize(string connectionString)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        if (!connectionString.Contains("GSS Encryption Mode", StringComparison.OrdinalIgnoreCase)
            && !connectionString.Contains("GssEncryptionMode", StringComparison.OrdinalIgnoreCase))
        {
            builder.GssEncryptionMode = GssEncryptionMode.Disable;
        }

        return builder.ConnectionString;
    }

    public static NpgsqlDataSource Create(string connectionString)
    {
        var builder = new NpgsqlDataSourceBuilder(Normalize(connectionString));
        builder.AddTypeInfoResolverFactory(new LegacyDateAndTimeResolverFactory());
        return builder.Build();
    }
}
