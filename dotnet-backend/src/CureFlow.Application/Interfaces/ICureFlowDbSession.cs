namespace CureFlow.Application.Interfaces;

/// <summary>
/// Scoped Dapper database session. Replaces runtime EF Core <c>ApplicationDbContext</c>.
/// DB operations honor cancellation and keep transaction-local tenant settings
/// active until their transaction commits or rolls back.
/// </summary>
public interface ICureFlowDbSession
{
    /// <summary>Current tenant from <see cref="Common.ITenantContext"/>.</summary>
    Guid TenantId { get; }

    /// <summary>Current user id when authenticated.</summary>
    Guid? UserId { get; }
    bool IsInTransaction => false;

    Task<IReadOnlyList<T>> QueryAsync<T>(
        string sql,
        object? param = null,
        bool ignoreTenant = false,
        CancellationToken ct = default);

    Task<T?> QueryFirstOrDefaultAsync<T>(
        string sql,
        object? param = null,
        bool ignoreTenant = false,
        CancellationToken ct = default);

    Task<T> QuerySingleAsync<T>(
        string sql,
        object? param = null,
        bool ignoreTenant = false,
        CancellationToken ct = default);

    Task<int> ExecuteAsync(
        string sql,
        object? param = null,
        bool ignoreTenant = false,
        CancellationToken ct = default);

    /// <summary>Insert a row; sets audit + tenant columns via EntityPersistence.</summary>
    Task InsertAsync<T>(T entity, bool ignoreTenant = false, CancellationToken ct = default)
        where T : Domain.Common.BaseEntity;

    /// <summary>Update all mapped columns on a row by primary key.</summary>
    Task UpdateAsync<T>(T entity, bool ignoreTenant = false, CancellationToken ct = default)
        where T : Domain.Common.BaseEntity;

    Task<T?> GetByIdAsync<T>(Guid id, bool ignoreTenant = false, CancellationToken ct = default)
        where T : Domain.Common.BaseEntity, new();

    Task<IReadOnlyList<T>> GetAllAsync<T>(string? extraWhere = null, object? param = null, bool ignoreTenant = false, CancellationToken ct = default)
        where T : Domain.Common.BaseEntity, new();

    /// <summary>Run multiple statements in one transaction.</summary>
    Task TransactionAsync(Func<ICureFlowDbSession, Task> action, CancellationToken ct = default, bool ignoreTenant = false);
}
