using System.Collections;
using System.Reflection;
using System.Text;
using CureFlow.Application.Common;
using CureFlow.Application.Interfaces;
using CureFlow.Domain.Common;
using Dapper;
using Npgsql;

namespace CureFlow.Infrastructure.Persistence.Dapper;

public class CureFlowDbSession : ICureFlowDbSession
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly DatabaseAuthority _authority;
    private readonly ITenantContext _tenant;
    private readonly NpgsqlConnection? _connection;
    private readonly NpgsqlTransaction? _transaction;
    private readonly Guid? _scopeTenantId;
    private readonly Guid? _scopeUserId;

    public CureFlowDbSession(NpgsqlDataSource dataSource, ITenantContext tenant) : this(dataSource, tenant, DatabaseAuthority.Tenant) { }

    internal CureFlowDbSession(NpgsqlDataSource dataSource, ITenantContext tenant, DatabaseAuthority authority)
    {
        DapperSetup.EnsureInitialized();
        _dataSource = dataSource;
        _tenant = tenant;
        _authority = authority;
    }

    private CureFlowDbSession(
        NpgsqlDataSource dataSource,
        ITenantContext tenant,
        NpgsqlConnection connection,
        NpgsqlTransaction transaction, DatabaseAuthority authority)
    {
        DapperSetup.EnsureInitialized();
        _dataSource = dataSource;
        _tenant = tenant;
        _authority = authority;
        _connection = connection;
        _transaction = transaction;
        _scopeTenantId = tenant.TenantId;
        _scopeUserId = tenant.UserId;
    }

    public Guid TenantId => _scopeTenantId ?? _tenant.TenantId;

    public Guid? UserId => _tenant.UserId;
    public bool IsInTransaction => _transaction is not null;

    public async Task<IReadOnlyList<T>> QueryAsync<T>(
        string sql,
        object? param = null,
        bool ignoreTenant = false,
        CancellationToken ct = default)
    {
        var parameters = MergeParams(param, ignoreTenant);
        await using var scope = await OpenScopeAsync(ignoreTenant, ct);
        var command = new CommandDefinition(
            sql,
            parameters,
            scope.Transaction,
            cancellationToken: ct);
        var rows = await scope.Connection.QueryAsync<T>(command);
        await scope.CompleteAsync(ct);
        return rows.AsList();
    }

    public async Task<T?> QueryFirstOrDefaultAsync<T>(
        string sql,
        object? param = null,
        bool ignoreTenant = false,
        CancellationToken ct = default)
    {
        var parameters = MergeParams(param, ignoreTenant);
        await using var scope = await OpenScopeAsync(ignoreTenant, ct);
        var command = new CommandDefinition(
            sql,
            parameters,
            scope.Transaction,
            cancellationToken: ct);
        var result = await scope.Connection.QueryFirstOrDefaultAsync<T>(command);
        await scope.CompleteAsync(ct);
        return result;
    }

    public async Task<T> QuerySingleAsync<T>(
        string sql,
        object? param = null,
        bool ignoreTenant = false,
        CancellationToken ct = default)
    {
        var parameters = MergeParams(param, ignoreTenant);
        await using var scope = await OpenScopeAsync(ignoreTenant, ct);
        var command = new CommandDefinition(
            sql,
            parameters,
            scope.Transaction,
            cancellationToken: ct);
        var result = await scope.Connection.QuerySingleAsync<T>(command);
        await scope.CompleteAsync(ct);
        return result;
    }

    public async Task<int> ExecuteAsync(
        string sql,
        object? param = null,
        bool ignoreTenant = false,
        CancellationToken ct = default)
    {
        var parameters = MergeParams(param, ignoreTenant);
        await using var scope = await OpenScopeAsync(ignoreTenant, ct);
        var command = new CommandDefinition(
            sql,
            parameters,
            scope.Transaction,
            cancellationToken: ct);
        var result = await scope.Connection.ExecuteAsync(command);
        await scope.CompleteAsync(ct);
        return result;
    }

    public async Task InsertAsync<T>(T entity, bool ignoreTenant = false, CancellationToken ct = default)
        where T : BaseEntity
    {
        ValidateEntityScope(entity, ignoreTenant);
        EntityPersistence.PrepareInsert(entity, _tenant, ignoreTenant);

        var table = TableMap.GetTableName<T>();
        var columns = GetColumnProperties(typeof(T)).ToList();
        var columnList = string.Join(", ", columns.Select(p => Quote(p.Name)));
        var valueList = string.Join(", ", columns.Select(p => "@" + p.Name));
        var sql = $"""INSERT INTO "{table}" ({columnList}) VALUES ({valueList})""";

        var param = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var column in columns)
            param[column.Name] = column.GetValue(entity);

        await ExecuteEntityMutationAsync(entity, "create", sql, param, ignoreTenant, ct);
    }

    public async Task UpdateAsync<T>(T entity, bool ignoreTenant = false, CancellationToken ct = default)
        where T : BaseEntity
    {
        ValidateEntityScope(entity, ignoreTenant);
        EntityPersistence.PrepareUpdate(entity, _tenant);

        var table = TableMap.GetTableName<T>();
        var columns = GetColumnProperties(typeof(T)).Where(p => p.Name != nameof(BaseEntity.Id)).ToList();
        var setClause = string.Join(", ", columns.Select(p => $"{Quote(p.Name)} = @{p.Name}"));

        var where = new StringBuilder($"""WHERE {Quote(nameof(BaseEntity.Id))} = @Id AND {SqlFragments.SoftDelete}""");
        if (typeof(TenantEntity).IsAssignableFrom(typeof(T)) && !ignoreTenant)
            where.Append(' ').Append(SqlFragments.TenantFilter);

        var sql = $"""UPDATE "{table}" SET {setClause} {where}""";

        var param = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [nameof(BaseEntity.Id)] = entity.Id,
        };
        foreach (var column in columns)
            param[column.Name] = column.GetValue(entity);

        await ExecuteEntityMutationAsync(entity, "update", sql, param, ignoreTenant, ct);
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

    public async Task TransactionAsync(Func<ICureFlowDbSession, Task> action, CancellationToken ct = default, bool ignoreTenant = false)
    {
        ValidateScope(ignoreTenant);
        ct.ThrowIfCancellationRequested();
        if (_connection is not null)
        {
            // The outer call owns commit/rollback and connection disposal.
            await action(this);
            return;
        }

        await using var scope = await OpenScopeAsync(ignoreTenant, ct);
        var session = new CureFlowDbSession(_dataSource, _tenant, scope.Connection, scope.Transaction!, _authority);
        await action(session);
        await scope.CompleteAsync(ct);
    }

    private void ValidateScope(bool ignoreTenant)
    {
        if (_scopeTenantId.HasValue && _scopeTenantId.Value != _tenant.TenantId)
            throw new DomainException("Tenant identity changed during the transaction.", 403);
        if (_scopeTenantId.HasValue && _scopeUserId != _tenant.UserId)
            throw new DomainException("Actor identity changed during the transaction.", 403);
        if (_authority == DatabaseAuthority.Platform && (!_tenant.IsPlatformUser || !_tenant.IsAuthenticated || !_tenant.UserId.HasValue || _tenant.TenantId != Guid.Empty))
            throw new DomainException("Platform operator identity is required.", 403);
        if (_authority == DatabaseAuthority.Tenant && _tenant.TenantId == Guid.Empty)
            throw new DomainException("Tenant identity is required for this database operation.", 403);
    }

    private void ValidateEntityScope(BaseEntity entity, bool ignoreTenant)
    {
        ValidateScope(ignoreTenant);
        if (entity is TenantEntity tenantEntity && _tenant.TenantId != Guid.Empty
            && tenantEntity.TenantId != Guid.Empty && tenantEntity.TenantId != _tenant.TenantId)
            throw new DomainException("Tenant identity does not match the current scope.", 403);
    }

    private object? MergeParams(object? param, bool ignoreTenant)
    {
        ValidateScope(ignoreTenant);
        if (_tenant.TenantId == Guid.Empty)
            return param;

        var merged = param is DynamicParameters dynamicParams
            ? dynamicParams.ParameterNames.ToDictionary(name => name, name => dynamicParams.Get<object?>(name), StringComparer.OrdinalIgnoreCase)
            : SqlParam.Merge(param);
        if (merged.TryGetValue("TenantId", out var supplied)
            && (!Guid.TryParse(supplied?.ToString(), out var tenantId) || tenantId != _tenant.TenantId))
            throw new DomainException("Tenant identity does not match the current scope.", 403);
        merged["TenantId"] = _tenant.TenantId;
        return merged;
    }

    private async Task<ConnectionScope> OpenScopeAsync(bool ignoreTenant, CancellationToken ct)
    {
        ValidateScope(ignoreTenant);
        ct.ThrowIfCancellationRequested();
        if (_connection is not null)
            return new ConnectionScope(_connection, _transaction!, ownsConnection: false);

        var connection = await _dataSource.OpenConnectionAsync(ct);
        NpgsqlTransaction? transaction = null;
        try
        {
            transaction = await connection.BeginTransactionAsync(ct);
            if (_authority != DatabaseAuthority.Provisioning)
                await DatabaseRoleBoundary.ValidateAsync(connection, transaction, _authority, ct);
            await PostgresRlsSession.ConfigureAsync(connection, _tenant.TenantId,
                platformBypass: _authority != DatabaseAuthority.Tenant && _tenant.TenantId == Guid.Empty, ct: ct, transaction: transaction,
                actorId: _authority == DatabaseAuthority.Tenant && _tenant.IsAuthenticated && !_tenant.IsPlatformUser
                    && string.IsNullOrEmpty(_tenant.ServiceIdentity) ? _tenant.UserId : null);
            return new ConnectionScope(connection, transaction, ownsConnection: true);
        }
        catch
        {
            try
            {
                if (transaction is not null) await transaction.DisposeAsync();
            }
            finally
            {
                await connection.DisposeAsync();
            }
            throw;
        }
    }

    private async Task ExecuteEntityMutationAsync(BaseEntity entity, string operation, string sql, object param, bool ignoreTenant, CancellationToken ct)
    {
        var parameters = MergeParams(param, ignoreTenant);
        await using var scope = await OpenScopeAsync(ignoreTenant, ct);
        var affected = await scope.Connection.ExecuteAsync(new CommandDefinition(sql, parameters, scope.Transaction, cancellationToken: ct));
        // Explicit clinical entity hooks; no SQL parsing, request/body logging or PHI snapshots.
        var clinical = entity is CureFlow.Domain.Entities.Patient or CureFlow.Domain.Entities.Visit or CureFlow.Domain.Entities.Appointment
            || (entity is TenantEntity && entity.GetType().Namespace is "CureFlow.Domain.Entities.Ehr" or "CureFlow.Domain.Entities.Lifestyle"
                && entity is not CureFlow.Domain.Entities.Ehr.ClinicalAuditEvent && entity is not CureFlow.Domain.Entities.Ehr.PrivateDocumentVersion);
        if (affected > 0 && clinical && _authority == DatabaseAuthority.Tenant)
        {
            var patientId = entity is CureFlow.Domain.Entities.Patient ? entity.Id
                : entity.GetType().GetProperty("PatientId")?.GetValue(entity) as Guid?;
            var revision = (entity.GetType().GetProperty("Revision")?.GetValue(entity) ?? entity.GetType().GetProperty("StorageVersion")?.GetValue(entity))?.ToString();
            var audit = CureFlow.Infrastructure.Auditing.ClinicalAccessAuditService.Build(_tenant,
                new ClinicalAccessEvidence(patientId, entity.GetType().Name, entity.Id, entity.IsDeleted ? "soft_delete" : operation, "committed", revision, _tenant.UserId.HasValue ? "care" : "operations"));
            EntityPersistence.PrepareInsert(audit, _tenant);
            var statement = EntityPersistence.BuildInsert(audit);
            await scope.Connection.ExecuteAsync(new CommandDefinition(statement.Sql, statement.Parameters, scope.Transaction, cancellationToken: ct));
        }
        await scope.CompleteAsync(ct);
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

        public ConnectionScope(NpgsqlConnection connection, NpgsqlTransaction transaction, bool ownsConnection)
        {
            _connection = connection;
            Transaction = transaction;
            _ownsConnection = ownsConnection;
        }

        public NpgsqlConnection Connection => _connection;

        public NpgsqlTransaction Transaction { get; }

        public Task CompleteAsync(CancellationToken ct) =>
            _ownsConnection ? Transaction.CommitAsync(ct) : Task.CompletedTask;

        public async ValueTask DisposeAsync()
        {
            if (!_ownsConnection) return;
            try
            {
                // Disposal rolls back any transaction whose command or commit failed.
                await Transaction.DisposeAsync();
            }
            finally
            {
                await _connection.DisposeAsync();
            }
        }
    }
}
