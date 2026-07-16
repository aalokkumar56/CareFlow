using System.Collections;
using System.Reflection;
using System.Text;
using CureFlow.Application.Common;
using CureFlow.Application.Interfaces;
using CureFlow.Domain.Common;
using Dapper;
using Npgsql;

namespace CureFlow.Infrastructure.Persistence.Dapper;

public sealed class CureFlowDbSession : ICureFlowDbSession
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly ITenantContext _tenant;
    private readonly NpgsqlConnection? _connection;
    private readonly NpgsqlTransaction? _transaction;

    public CureFlowDbSession(NpgsqlDataSource dataSource, ITenantContext tenant)
    {
        DapperSetup.EnsureInitialized();
        _dataSource = dataSource;
        _tenant = tenant;
    }

    private CureFlowDbSession(
        NpgsqlDataSource dataSource,
        ITenantContext tenant,
        NpgsqlConnection connection,
        NpgsqlTransaction transaction)
    {
        DapperSetup.EnsureInitialized();
        _dataSource = dataSource;
        _tenant = tenant;
        _connection = connection;
        _transaction = transaction;
    }

    public Guid TenantId => _tenant.TenantId;

    public Guid? UserId => _tenant.UserId;

    public async Task<IReadOnlyList<T>> QueryAsync<T>(
        string sql,
        object? param = null,
        bool ignoreTenant = false,
        CancellationToken ct = default)
    {
        await using var scope = await OpenScopeAsync(ignoreTenant, ct);
        var command = new CommandDefinition(
            sql,
            MergeParams(param, ignoreTenant),
            scope.Transaction,
            cancellationToken: CancellationToken.None);
        var rows = await scope.Connection.QueryAsync<T>(command);
        return rows.AsList();
    }

    public async Task<T?> QueryFirstOrDefaultAsync<T>(
        string sql,
        object? param = null,
        bool ignoreTenant = false,
        CancellationToken ct = default)
    {
        await using var scope = await OpenScopeAsync(ignoreTenant, ct);
        var command = new CommandDefinition(
            sql,
            MergeParams(param, ignoreTenant),
            scope.Transaction,
            cancellationToken: CancellationToken.None);
        return await scope.Connection.QueryFirstOrDefaultAsync<T>(command);
    }

    public async Task<T> QuerySingleAsync<T>(
        string sql,
        object? param = null,
        bool ignoreTenant = false,
        CancellationToken ct = default)
    {
        await using var scope = await OpenScopeAsync(ignoreTenant, ct);
        var command = new CommandDefinition(
            sql,
            MergeParams(param, ignoreTenant),
            scope.Transaction,
            cancellationToken: CancellationToken.None);
        return await scope.Connection.QuerySingleAsync<T>(command);
    }

    public async Task<int> ExecuteAsync(
        string sql,
        object? param = null,
        bool ignoreTenant = false,
        CancellationToken ct = default)
    {
        await using var scope = await OpenScopeAsync(ignoreTenant, ct);
        var command = new CommandDefinition(
            sql,
            MergeParams(param, ignoreTenant),
            scope.Transaction,
            cancellationToken: CancellationToken.None);
        return await scope.Connection.ExecuteAsync(command);
    }

    public async Task InsertAsync<T>(T entity, bool ignoreTenant = false, CancellationToken ct = default)
        where T : BaseEntity
    {
        EntityPersistence.PrepareInsert(entity, _tenant, ignoreTenant);

        var table = TableMap.GetTableName<T>();
        var columns = GetColumnProperties(typeof(T)).ToList();
        var columnList = string.Join(", ", columns.Select(p => Quote(p.Name)));
        var valueList = string.Join(", ", columns.Select(p => "@" + p.Name));
        var sql = $"""INSERT INTO "{table}" ({columnList}) VALUES ({valueList})""";

        await ExecuteAsync(sql, entity, ignoreTenant, ct);
    }

    public async Task UpdateAsync<T>(T entity, bool ignoreTenant = false, CancellationToken ct = default)
        where T : BaseEntity
    {
        EntityPersistence.PrepareUpdate(entity, _tenant);

        var table = TableMap.GetTableName<T>();
        var columns = GetColumnProperties(typeof(T)).Where(p => p.Name != nameof(BaseEntity.Id)).ToList();
        var setClause = string.Join(", ", columns.Select(p => $"{Quote(p.Name)} = @{p.Name}"));

        var where = new StringBuilder($"""WHERE {Quote(nameof(BaseEntity.Id))} = @Id AND {SqlFragments.SoftDelete}""");
        if (typeof(TenantEntity).IsAssignableFrom(typeof(T)) && !ignoreTenant)
            where.Append(' ').Append(SqlFragments.TenantFilter);

        var sql = $"""UPDATE "{table}" SET {setClause} {where}""";
        await ExecuteAsync(sql, entity, ignoreTenant, ct);
    }

    public async Task<T?> GetByIdAsync<T>(Guid id, bool ignoreTenant = false, CancellationToken ct = default)
        where T : BaseEntity, new()
    {
        var table = TableMap.GetTableName<T>();
        var where = SqlFragments.WhereActive<T>(ignoreTenant);
        var sql = $"""SELECT * FROM "{table}" WHERE {Quote(nameof(BaseEntity.Id))} = @Id AND {where}""";
        return await QueryFirstOrDefaultAsync<T>(sql, new { Id = id }, ignoreTenant, ct);
    }

    public async Task<IReadOnlyList<T>> GetAllAsync<T>(
        string? extraWhere = null,
        object? param = null,
        bool ignoreTenant = false,
        CancellationToken ct = default)
        where T : BaseEntity, new()
    {
        var table = TableMap.GetTableName<T>();
        var where = SqlFragments.WhereActive<T>(ignoreTenant);
        if (!string.IsNullOrWhiteSpace(extraWhere))
            where += " AND (" + extraWhere + ")";

        var sql = $"""SELECT * FROM "{table}" WHERE {where}""";
        return await QueryAsync<T>(sql, param, ignoreTenant, ct);
    }

    public async Task TransactionAsync(Func<ICureFlowDbSession, Task> action, CancellationToken ct = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(CancellationToken.None);
        await PostgresRlsSession.ConfigureAsync(
            connection,
            _tenant.TenantId,
            platformBypass: _tenant.TenantId == Guid.Empty,
            ct: CancellationToken.None);
        await using var transaction = await connection.BeginTransactionAsync(CancellationToken.None);

        var session = new CureFlowDbSession(_dataSource, _tenant, connection, transaction);
        try
        {
            await action(session);
            await transaction.CommitAsync(CancellationToken.None);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private object? MergeParams(object? param, bool ignoreTenant)
    {
        if (ignoreTenant || _tenant.TenantId == Guid.Empty)
            return param;

        if (param is null)
            return new { TenantId = _tenant.TenantId };

        if (param is IDictionary<string, object?> dict)
        {
            if (!dict.ContainsKey("TenantId"))
                dict["TenantId"] = _tenant.TenantId;
            return dict;
        }

        var merged = SqlParam.Merge(param);
        if (!merged.ContainsKey("TenantId"))
            merged["TenantId"] = _tenant.TenantId;
        return merged;
    }

    private async Task<ConnectionScope> OpenScopeAsync(bool ignoreTenant, CancellationToken ct)
    {
        if (_connection is not null)
            return new ConnectionScope(_connection, _transaction, ownsConnection: false);

        // RLS setup must complete before any tenant-scoped SQL and before the
        // connection returns to the pool. Use CancellationToken.None here (same as
        // TransactionAsync) so aborted HTTP requests do not leave pooled connections
        // in a half-configured state or surface OperationCanceledException as 500s.
        var connection = await _dataSource.OpenConnectionAsync(CancellationToken.None);
        var platformBypass = ignoreTenant && _tenant.TenantId == Guid.Empty;
        await PostgresRlsSession.ConfigureAsync(connection, _tenant.TenantId, platformBypass, CancellationToken.None);
        return new ConnectionScope(connection, null, ownsConnection: true);
    }

    private static string Quote(string identifier) => "\"" + identifier + "\"";

    private static IEnumerable<PropertyInfo> GetColumnProperties(Type entityType)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var properties = new List<PropertyInfo>();

        for (var type = entityType; type != null && type != typeof(object); type = type.BaseType)
        {
            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                if (!property.CanRead || !property.CanWrite || IsNavigation(property) || !seen.Add(property.Name))
                    continue;

                properties.Add(property);
            }
        }

        return properties;
    }

    private static bool IsNavigation(PropertyInfo property)
    {
        var type = property.PropertyType;

        if (typeof(BaseEntity).IsAssignableFrom(type))
            return true;

        if (type == typeof(string) || type == typeof(List<string>))
            return false;

        if (typeof(IEnumerable).IsAssignableFrom(type) && type != typeof(string))
            return true;

        return false;
    }

    private sealed class ConnectionScope : IAsyncDisposable
    {
        private readonly NpgsqlConnection _connection;
        private readonly bool _ownsConnection;

        public ConnectionScope(NpgsqlConnection connection, NpgsqlTransaction? transaction, bool ownsConnection)
        {
            _connection = connection;
            Transaction = transaction;
            _ownsConnection = ownsConnection;
        }

        public NpgsqlConnection Connection => _connection;

        public NpgsqlTransaction? Transaction { get; }

        public ValueTask DisposeAsync() =>
            _ownsConnection ? _connection.DisposeAsync() : ValueTask.CompletedTask;
    }
}
