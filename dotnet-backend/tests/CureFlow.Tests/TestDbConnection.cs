namespace CureFlow.Tests;

internal static class TestDbConnection
{
    public static string? Resolve() => TestDbHelper.ResolveConnectionString();
}
