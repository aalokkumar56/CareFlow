using System.Collections.Concurrent;
using System.Reflection;
using System.Text;
using CureFlow.Application.Common;
using CureFlow.Domain.Common;
using CureFlow.Domain.Entities;
using Dapper;

namespace CureFlow.Infrastructure.Persistence.Dapper;

public static class EntityPersistence
{
    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> PropertyCache = new();

    public static void PrepareInsert(BaseEntity entity, ITenantContext tenant, bool ignoreTenant = false)
    {
        var now = DateTime.UtcNow;
        if (entity.Id == Guid.Empty)
            entity.Id = Guid.NewGuid();
        entity.CreatedAt = now;
        entity.UpdatedAt = now;
        if (tenant.UserId.HasValue)
            entity.CreatedBy = tenant.UserId;

        if (!ignoreTenant && tenant.TenantId != Guid.Empty && entity is TenantEntity te && te.TenantId == Guid.Empty)
            te.TenantId = tenant.TenantId;

        NormalizeDateTimes(entity);
    }

    public static void PrepareUpdate(BaseEntity entity, ITenantContext tenant)
    {
        entity.UpdatedAt = DateTime.UtcNow;
        if (tenant.UserId.HasValue)
            entity.UpdatedBy = tenant.UserId;
        NormalizeDateTimes(entity);
    }

    public static (string Sql, DynamicParameters Parameters) BuildInsert<T>(T entity) where T : BaseEntity
    {
        var props = GetPersistedProperties(typeof(T));
        var table = TableMap.GetTableName<T>();
        var columns = props.Select(p => Quote(p.Name)).ToArray();
        var values = props.Select(p => "@" + p.Name).ToArray();
        var sql = $"""
            INSERT INTO "{table}" ({string.Join(", ", columns)})
            VALUES ({string.Join(", ", values)})
            """;
        return (sql, ToParameters(entity, props));
    }

    public static (string Sql, DynamicParameters Parameters) BuildUpdate<T>(T entity) where T : BaseEntity
    {
        var props = GetPersistedProperties(typeof(T))
            .Where(p => p.Name is not nameof(BaseEntity.Id)
                and not nameof(BaseEntity.CreatedAt)
                and not nameof(BaseEntity.CreatedBy))
            .ToArray();
        var table = TableMap.GetTableName<T>();
        var setClause = string.Join(", ", props.Select(p => $"{Quote(p.Name)} = @{p.Name}"));
        var sql = $"""
            UPDATE "{table}" SET {setClause}
            WHERE "Id" = @Id
            """;
        return (sql, ToParameters(entity, props));
    }

    private static PropertyInfo[] GetPersistedProperties(Type type) =>
        PropertyCache.GetOrAdd(type, t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(IsPersistedProperty)
            .OrderBy(p => p.Name)
            .ToArray());

    private static bool IsPersistedProperty(PropertyInfo property)
    {
        if (!property.CanRead || !property.CanWrite) return false;
        if (property.Name is nameof(BaseEntity.Id)) return true;

        var pt = property.PropertyType;
        if (pt == typeof(string) || pt == typeof(Guid) || pt == typeof(Guid?)
            || pt == typeof(int) || pt == typeof(int?) || pt == typeof(long) || pt == typeof(long?)
            || pt == typeof(bool) || pt == typeof(decimal) || pt == typeof(decimal?)
            || pt == typeof(DateTime) || pt == typeof(DateTime?)
            || pt == typeof(DateOnly) || pt == typeof(DateOnly?)
            || pt.IsEnum || Nullable.GetUnderlyingType(pt)?.IsEnum == true)
            return true;

        if (pt == typeof(List<string>)) return true;
        return false;
    }

    private static DynamicParameters ToParameters(BaseEntity entity, PropertyInfo[] props)
    {
        var dp = new DynamicParameters();
        foreach (var prop in props)
            dp.Add(prop.Name, prop.GetValue(entity));
        return dp;
    }

    private static void NormalizeDateTimes(BaseEntity entity)
    {
        foreach (var prop in GetPersistedProperties(entity.GetType()))
        {
            if (prop.PropertyType == typeof(DateTime) && prop.GetValue(entity) is DateTime dt)
                prop.SetValue(entity, DateTimeHelper.EnsureUtc(dt));
            else if (prop.PropertyType == typeof(DateTime?) && prop.GetValue(entity) is DateTime ndt)
                prop.SetValue(entity, DateTimeHelper.EnsureUtc(ndt));
        }
    }

    private static string Quote(string name) => $"\"{name}\"";
}
