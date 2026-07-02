using System.Data;

using System.Globalization;

using System.Reflection;

using System.Text.Json;

using Dapper;

using Npgsql;

using NpgsqlTypes;



namespace CureFlow.Infrastructure.Persistence.Dapper;



public static class DapperSetup

{

    private static volatile bool _initialized;

    private static readonly Lock InitLock = new();



    public static void Configure() => EnsureInitialized();



    public static void EnsureInitialized()

    {

        if (_initialized) return;

        lock (InitLock)

        {

            if (_initialized) return;



            DefaultTypeMap.MatchNamesWithUnderscores = false;



            SqlMapper.AddTypeMap(typeof(DateOnly), DbType.Date);

            SqlMapper.AddTypeMap(typeof(DateOnly?), DbType.Date);



            SqlMapper.AddTypeHandler(new JsonbListStringTypeHandler());

            SqlMapper.AddTypeHandler(new DateOnlyTypeHandler());

            SqlMapper.AddTypeHandler(new NullableDateOnlyTypeHandler());

            RegisterEnumHandlers(typeof(Domain.Entities.Patient).Assembly);



            _initialized = true;

        }

    }



    private static void RegisterEnumHandlers(Assembly assembly)

    {

        foreach (var enumType in assembly.GetTypes().Where(t => t.IsEnum))

            RegisterEnumHandler(enumType);

    }



    private static void RegisterEnumHandler(Type enumType)

    {

        var method = typeof(DapperSetup)

            .GetMethod(nameof(RegisterEnumHandlerGeneric), BindingFlags.NonPublic | BindingFlags.Static)!

            .MakeGenericMethod(enumType);

        method.Invoke(null, null);

    }



    private static void RegisterEnumHandlerGeneric<T>() where T : struct, Enum =>

        SqlMapper.AddTypeHandler(new EnumIntTypeHandler<T>());



    private sealed class JsonbListStringTypeHandler : SqlMapper.TypeHandler<List<string>>

    {

        public override List<string> Parse(object value) =>

            value switch

            {

                null => new List<string>(),

                List<string> list => list,

                string s => JsonSerializer.Deserialize<List<string>>(s) ?? new List<string>(),

                _ => new List<string>(),

            };



        public override void SetValue(IDbDataParameter parameter, List<string>? value)

        {

            parameter.Value = JsonSerializer.Serialize(value ?? new List<string>());

            if (parameter is NpgsqlParameter npgsql)

                npgsql.NpgsqlDbType = NpgsqlDbType.Jsonb;

        }

    }



    private sealed class DateOnlyTypeHandler : SqlMapper.TypeHandler<DateOnly>

    {

        public override DateOnly Parse(object value) => DateOnlyParsing.ToDateOnly(value);



        public override void SetValue(IDbDataParameter parameter, DateOnly value) =>

            DateOnlyParsing.SetParameter(parameter, value);

    }



    private sealed class NullableDateOnlyTypeHandler : SqlMapper.TypeHandler<DateOnly?>

    {

        public override DateOnly? Parse(object value) => DateOnlyParsing.ToNullableDateOnly(value);



        public override void SetValue(IDbDataParameter parameter, DateOnly? value) =>

            DateOnlyParsing.SetParameter(parameter, value);

    }



    private sealed class EnumIntTypeHandler<T> : SqlMapper.TypeHandler<T> where T : struct, Enum

    {

        public override T Parse(object value) =>

            (T)Enum.ToObject(typeof(T), Convert.ToInt32(value, CultureInfo.InvariantCulture));



        public override void SetValue(IDbDataParameter parameter, T value) =>

            parameter.Value = Convert.ToInt32(value, CultureInfo.InvariantCulture);

    }



    private static class DateOnlyParsing

    {

        private static readonly string[] DateFormats =

        [

            "yyyy-MM-dd",

            "dd-MM-yyyy",

            "dd/MM/yyyy",

            "MM/dd/yyyy",

            "M/d/yyyy",

        ];



        internal static DateOnly ToDateOnly(object value) =>

            value switch

            {

                DateOnly d => d,

                DateTime dt => DateOnly.FromDateTime(dt),

                DateTimeOffset dto => DateOnly.FromDateTime(dto.UtcDateTime),

                string s => ParseString(s),

                _ => TryConvert(value),

            };



        internal static DateOnly? ToNullableDateOnly(object value)

        {

            if (value is null or DBNull)

                return null;



            return ToDateOnly(value);

        }



        internal static void SetParameter(IDbDataParameter parameter, DateOnly value)

        {

            parameter.Value = value;

            parameter.DbType = DbType.Date;

            if (parameter is NpgsqlParameter npgsql)

                npgsql.NpgsqlDbType = NpgsqlDbType.Date;

        }



        internal static void SetParameter(IDbDataParameter parameter, DateOnly? value)

        {

            if (!value.HasValue)

            {

                parameter.Value = DBNull.Value;

                return;

            }



            SetParameter(parameter, value.Value);

        }



        private static DateOnly ParseString(string s)

        {

            if (string.IsNullOrWhiteSpace(s))

                throw new FormatException("Cannot parse empty string as DateOnly.");



            if (DateOnly.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var iso))

                return iso;



            if (DateOnly.TryParseExact(s, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var exact))

                return exact;



            throw new FormatException($"Cannot parse '{s}' as DateOnly.");

        }



        private static DateOnly TryConvert(object value)

        {

            var type = value.GetType();

            if (type == typeof(DateOnly))

                return (DateOnly)value;



            if (type.FullName == "NpgsqlTypes.NpgsqlDate" && value.ToString() is { } npgsqlDate)

                return ParseString(npgsqlDate);



            return DateOnly.FromDateTime(Convert.ToDateTime(value, CultureInfo.InvariantCulture));

        }

    }

}


