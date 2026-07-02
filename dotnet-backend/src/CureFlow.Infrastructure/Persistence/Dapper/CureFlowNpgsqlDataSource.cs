using Npgsql;

namespace CureFlow.Infrastructure.Persistence.Dapper;

public static class CureFlowNpgsqlDataSource
{
    public static NpgsqlDataSource Create(string connectionString)
    {
        var builder = new NpgsqlDataSourceBuilder(connectionString);
        builder.AddTypeInfoResolverFactory(new LegacyDateAndTimeResolverFactory());
        return builder.Build();
    }
}
