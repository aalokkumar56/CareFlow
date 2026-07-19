namespace CureFlow.UnitTests.Helpers;

internal static class TestDbConnection
{
    public static string? Resolve() => TestDbHelper.ResolveConnectionString();

    public static string Require() => TestDbHelper.RequireConnectionString();

    public static string RequireRlsSubject() => TestDbHelper.RequireRlsSubjectConnectionString();
}
