using System.Collections;
using System.Data;
using System.Data.Common;
using Dapper;
using FluentAssertions;
using Xunit;

namespace CureFlow.Tests;

// Regression guard for Npgsql error 42809 ("op ANY/ALL (array) requires array on right side").
//
// PostgreSQL's "col = ANY(@p)" requires @p to be sent as a real array. Dapper passes a
// CLR array (T[]) to Npgsql as a single native array parameter, but it EXPANDS a List<T>
// into "(@p1,@p2,...)" which turns "ANY(@p)" into the invalid "ANY((@p1,@p2))".
// ConversationService.ListAsync and EmailService.GetThreadsAsync regressed by passing
// List<T> to ANY(...); both were changed to arrays. These tests lock in that contract.
public class AnyArrayParameterTests
{
    [Fact]
    public async Task ArrayParameter_IsNotExpanded_SoAnyReceivesNativeArray()
    {
        await using var conn = new NpgsqlConnection();
        conn.Open();

        await conn.ExecuteAsync(
            """SELECT "Phone" FROM "Patients" WHERE "Phone" = ANY(@phones)""",
            new { phones = new[] { "a", "b", "c" } });

        // CommandText is left untouched: a single array parameter is bound.
        conn.LastCommandText.Should().Contain("ANY(@phones)");
        conn.LastCommandText.Should().NotContain("@phones1");
        conn.LastParameterNames.Should().ContainSingle().Which.Should().Be("phones");
        conn.LastParameterValues.Single().Should().BeOfType<string[]>();
    }

    [Fact]
    public async Task GuidArrayParameter_IsNotExpanded()
    {
        await using var conn = new NpgsqlConnection();
        conn.Open();

        await conn.ExecuteAsync(
            """SELECT "Id" FROM "Patients" WHERE "Id" = ANY(@patientIds)""",
            new { patientIds = new[] { Guid.NewGuid(), Guid.NewGuid() } });

        conn.LastCommandText.Should().Contain("ANY(@patientIds)");
        conn.LastParameterNames.Should().ContainSingle().Which.Should().Be("patientIds");
        conn.LastParameterValues.Single().Should().BeOfType<Guid[]>();
    }

    [Fact]
    public async Task ListStringWithJsonbTypeHandler_IsSentAsScalar_NotArray()
    {
        // Mirrors DapperSetup's global JsonbListStringTypeHandler: a List<string> parameter is
        // serialized to a single jsonb string. That scalar is precisely what made
        // ConversationService.ListAsync fail with 42809, because ANY(@phones) requires an array.
        // The fix switches phones to string[], which has no type handler and is bound as text[].
        SqlMapper.AddTypeHandler(new JsonbListStringHandler());

        await using var conn = new NpgsqlConnection();
        conn.Open();

        await conn.ExecuteAsync(
            """SELECT 1 WHERE 'x' = ANY(@phones)""",
            new { phones = new List<string> { "a", "b" } });

        conn.LastParameterNames.Should().ContainSingle().Which.Should().Be("phones");
        // Bound as a serialized jsonb scalar (string), NOT a text[] -> ANY(scalar) errors in PG.
        conn.LastParameterValues.Single().Should().BeOfType<string>();
    }

    private sealed class JsonbListStringHandler : SqlMapper.TypeHandler<List<string>>
    {
        public override List<string> Parse(object value) => new();
        public override void SetValue(IDbDataParameter parameter, List<string>? value) =>
            parameter.Value = System.Text.Json.JsonSerializer.Serialize(value ?? new List<string>());
    }

    // ---- Minimal fake ADO.NET provider ----------------------------------------------------------
    // Named "NpgsqlConnection" on purpose: Dapper's FeatureSupport matches this type name
    // (case-insensitive) to enable PostgreSQL native-array parameter handling.
    private sealed class NpgsqlConnection : DbConnection
    {
        private ConnectionState _state = ConnectionState.Closed;

        public string? LastCommandText { get; private set; }
        public List<string> LastParameterNames { get; } = new();
        public List<object?> LastParameterValues { get; } = new();

        internal void Capture(FakeCommand command)
        {
            LastCommandText = command.CommandText;
            LastParameterNames.Clear();
            LastParameterValues.Clear();
            foreach (FakeParameter p in command.Parameters)
            {
                LastParameterNames.Add(p.ParameterName);
                LastParameterValues.Add(p.Value);
            }
        }

        public override string ConnectionString { get; set; } = "";
        public override string Database => "fake";
        public override string DataSource => "fake";
        public override string ServerVersion => "16.0";
        public override ConnectionState State => _state;
        public override void ChangeDatabase(string databaseName) { }
        public override void Close() => _state = ConnectionState.Closed;
        public override void Open() => _state = ConnectionState.Open;
        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) =>
            throw new NotSupportedException();
        protected override DbCommand CreateDbCommand() => new FakeCommand(this);
    }

    private sealed class FakeCommand : DbCommand
    {
        private readonly NpgsqlConnection _conn;
        private readonly FakeParameterCollection _parameters = new();

        public FakeCommand(NpgsqlConnection conn)
        {
            _conn = conn;
            // Dapper's FeatureSupport inspects command.Connection's type name to enable
            // PostgreSQL native-array parameter handling, so it must be wired up here.
            DbConnection = conn;
        }

        public override string CommandText { get; set; } = "";
        public override int CommandTimeout { get; set; }
        public override CommandType CommandType { get; set; }
        public override UpdateRowSource UpdatedRowSource { get; set; }
        public override bool DesignTimeVisible { get; set; }
        protected override DbConnection? DbConnection { get; set; }
        protected override DbParameterCollection DbParameterCollection => _parameters;
        protected override DbTransaction? DbTransaction { get; set; }

        public override void Cancel() { }
        public override void Prepare() { }
        public override int ExecuteNonQuery()
        {
            _conn.Capture(this);
            return 0;
        }
        public override object? ExecuteScalar()
        {
            _conn.Capture(this);
            return null;
        }
        protected override DbParameter CreateDbParameter() => new FakeParameter();
        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior)
        {
            _conn.Capture(this);
            throw new NotSupportedException("Tests use ExecuteAsync only.");
        }
    }

    private sealed class FakeParameter : DbParameter
    {
        public override DbType DbType { get; set; }
        public override ParameterDirection Direction { get; set; }
        public override bool IsNullable { get; set; }
        public override string ParameterName { get; set; } = "";
        public override int Size { get; set; }
        public override string SourceColumn { get; set; } = "";
        public override bool SourceColumnNullMapping { get; set; }
        public override object? Value { get; set; }
        public override void ResetDbType() { }
    }

    private sealed class FakeParameterCollection : DbParameterCollection
    {
        private readonly List<FakeParameter> _items = new();

        public override int Count => _items.Count;
        public override object SyncRoot => _items;

        public override int Add(object value)
        {
            _items.Add((FakeParameter)value);
            return _items.Count - 1;
        }
        public override void AddRange(Array values)
        {
            foreach (var v in values) Add(v!);
        }
        public override void Clear() => _items.Clear();
        public override bool Contains(object value) => _items.Contains((FakeParameter)value);
        public override bool Contains(string value) => IndexOf(value) >= 0;
        public override void CopyTo(Array array, int index) => ((ICollection)_items).CopyTo(array, index);
        public override IEnumerator GetEnumerator() => _items.GetEnumerator();
        public override int IndexOf(object value) => _items.IndexOf((FakeParameter)value);
        public override int IndexOf(string parameterName) =>
            _items.FindIndex(p => p.ParameterName == parameterName);
        public override void Insert(int index, object value) => _items.Insert(index, (FakeParameter)value);
        public override void Remove(object value) => _items.Remove((FakeParameter)value);
        public override void RemoveAt(int index) => _items.RemoveAt(index);
        public override void RemoveAt(string parameterName) => RemoveAt(IndexOf(parameterName));
        protected override DbParameter GetParameter(int index) => _items[index];
        protected override DbParameter GetParameter(string parameterName) => _items[IndexOf(parameterName)];
        protected override void SetParameter(int index, DbParameter value) => _items[index] = (FakeParameter)value;
        protected override void SetParameter(string parameterName, DbParameter value) =>
            _items[IndexOf(parameterName)] = (FakeParameter)value;
    }
}
