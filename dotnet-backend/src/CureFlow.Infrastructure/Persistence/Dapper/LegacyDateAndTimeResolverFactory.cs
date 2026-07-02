using Npgsql.Internal;
using Npgsql.Internal.Postgres;
using NpgsqlTypes;

namespace CureFlow.Infrastructure.Persistence.Dapper;

/// <summary>
/// Restores Npgsql 9 behavior: PostgreSQL date/time columns read as DateTime/TimeSpan via GetValue().
/// Required for Dapper, which does not map Npgsql 10's default DateOnly from GetValue() reliably.
/// </summary>
/// <seealso href="https://www.npgsql.org/doc/release-notes/10.0.html#date-and-time-are-now-mapped-to-dateonly-and-timeonly"/>
internal sealed class LegacyDateAndTimeResolverFactory : PgTypeInfoResolverFactory
{
    public override IPgTypeInfoResolver CreateResolver() => new Resolver();
    public override IPgTypeInfoResolver CreateArrayResolver() => new ArrayResolver();
    public override IPgTypeInfoResolver CreateRangeResolver() => new RangeResolver();
    public override IPgTypeInfoResolver CreateRangeArrayResolver() => new RangeArrayResolver();
    public override IPgTypeInfoResolver CreateMultirangeResolver() => new MultirangeResolver();
    public override IPgTypeInfoResolver CreateMultirangeArrayResolver() => new MultirangeArrayResolver();

    const string Date = "pg_catalog.date";
    const string Time = "pg_catalog.time";
    const string DateRange = "pg_catalog.daterange";
    const string DateMultirange = "pg_catalog.datemultirange";

    class Resolver : IPgTypeInfoResolver
    {
        TypeInfoMappingCollection? _mappings;
        protected TypeInfoMappingCollection Mappings => _mappings ??= AddMappings(new());

        public PgTypeInfo? GetTypeInfo(Type? type, DataTypeName? dataTypeName, PgSerializerOptions options)
            => type == typeof(object) ? Mappings.Find(type, dataTypeName, options) : null;

        static TypeInfoMappingCollection AddMappings(TypeInfoMappingCollection mappings)
        {
            mappings.AddStructType<DateTime>(Date,
                static (options, mapping, _) => options.GetTypeInfo(typeof(DateTime), new DataTypeName(mapping.DataTypeName))!,
                matchRequirement: MatchRequirement.DataTypeName);

            mappings.AddStructType<TimeSpan>(Time,
                static (options, mapping, _) => options.GetTypeInfo(typeof(TimeSpan), new DataTypeName(mapping.DataTypeName))!,
                isDefault: true);

            return mappings;
        }
    }

    sealed class ArrayResolver : Resolver, IPgTypeInfoResolver
    {
        TypeInfoMappingCollection? _mappings;
        new TypeInfoMappingCollection Mappings => _mappings ??= AddMappings(new(base.Mappings));

        public new PgTypeInfo? GetTypeInfo(Type? type, DataTypeName? dataTypeName, PgSerializerOptions options)
            => type == typeof(object) ? Mappings.Find(type, dataTypeName, options) : null;

        static TypeInfoMappingCollection AddMappings(TypeInfoMappingCollection mappings)
        {
            mappings.AddStructArrayType<DateTime>(Date);
            mappings.AddStructArrayType<TimeSpan>(Time);
            return mappings;
        }
    }

    class RangeResolver : IPgTypeInfoResolver
    {
        TypeInfoMappingCollection? _mappings;
        protected TypeInfoMappingCollection Mappings => _mappings ??= AddMappings(new());

        public PgTypeInfo? GetTypeInfo(Type? type, DataTypeName? dataTypeName, PgSerializerOptions options)
            => type == typeof(object) ? Mappings.Find(type, dataTypeName, options) : null;

        static TypeInfoMappingCollection AddMappings(TypeInfoMappingCollection mappings)
        {
            mappings.AddStructType<NpgsqlRange<DateTime>>(DateRange,
                static (options, mapping, _) => options.GetTypeInfo(typeof(NpgsqlRange<DateTime>), new DataTypeName(mapping.DataTypeName))!,
                matchRequirement: MatchRequirement.DataTypeName);

            return mappings;
        }
    }

    sealed class RangeArrayResolver : RangeResolver, IPgTypeInfoResolver
    {
        TypeInfoMappingCollection? _mappings;
        new TypeInfoMappingCollection Mappings => _mappings ??= AddMappings(new(base.Mappings));

        public new PgTypeInfo? GetTypeInfo(Type? type, DataTypeName? dataTypeName, PgSerializerOptions options)
            => type == typeof(object) ? Mappings.Find(type, dataTypeName, options) : null;

        static TypeInfoMappingCollection AddMappings(TypeInfoMappingCollection mappings)
        {
            mappings.AddStructArrayType<NpgsqlRange<DateTime>>(DateRange);
            return mappings;
        }
    }

    class MultirangeResolver : IPgTypeInfoResolver
    {
        TypeInfoMappingCollection? _mappings;
        protected TypeInfoMappingCollection Mappings => _mappings ??= AddMappings(new());

        public PgTypeInfo? GetTypeInfo(Type? type, DataTypeName? dataTypeName, PgSerializerOptions options)
            => type == typeof(object) ? Mappings.Find(type, dataTypeName, options) : null;

        static TypeInfoMappingCollection AddMappings(TypeInfoMappingCollection mappings)
        {
            mappings.AddType<NpgsqlRange<DateTime>[]>(DateMultirange,
                static (options, mapping, _) => options.GetTypeInfo(typeof(NpgsqlRange<DateTime>[]), new DataTypeName(mapping.DataTypeName))!,
                matchRequirement: MatchRequirement.DataTypeName);

            return mappings;
        }
    }

    sealed class MultirangeArrayResolver : MultirangeResolver, IPgTypeInfoResolver
    {
        TypeInfoMappingCollection? _mappings;
        new TypeInfoMappingCollection Mappings => _mappings ??= AddMappings(new(base.Mappings));

        public new PgTypeInfo? GetTypeInfo(Type? type, DataTypeName? dataTypeName, PgSerializerOptions options)
            => type == typeof(object) ? Mappings.Find(type, dataTypeName, options) : null;

        static TypeInfoMappingCollection AddMappings(TypeInfoMappingCollection mappings)
        {
            mappings.AddArrayType<NpgsqlRange<DateTime>[]>(DateMultirange);
            return mappings;
        }
    }
}
