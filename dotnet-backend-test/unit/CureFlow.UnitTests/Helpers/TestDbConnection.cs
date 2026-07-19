namespace CureFlow.UnitTests;

internal static class TestDbConnection
{
    public static string? Resolve() => TestDbHelper.ResolveConnectionString();
}
