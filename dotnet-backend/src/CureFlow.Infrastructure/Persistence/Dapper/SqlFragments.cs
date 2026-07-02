using CureFlow.Domain.Common;

namespace CureFlow.Infrastructure.Persistence.Dapper;

public static class SqlFragments
{
    public const string SoftDelete = @"""IsDeleted"" = false";

    public const string TenantFilter = @"AND ""TenantId"" = @TenantId";

    public static string WhereActive<T>(bool ignoreTenant)
    {
        if (ignoreTenant) return "1=1";

        if (typeof(TenantEntity).IsAssignableFrom(typeof(T)))
            return $@"{SoftDelete} AND ""TenantId"" = @TenantId";

        if (typeof(BaseEntity).IsAssignableFrom(typeof(T)))
            return SoftDelete;

        return "1=1";
    }
}
