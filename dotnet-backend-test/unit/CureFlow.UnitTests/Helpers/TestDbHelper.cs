namespace CureFlow.UnitTests;

internal static class TestDbHelper
{
    public static string? ResolveConnectionString()
    {
        var env = Environment.GetEnvironmentVariable("CUREFLOW_TEST_CONNECTION");
        if (!string.IsNullOrWhiteSpace(env))
            return env;

        // From unit/CureFlow.UnitTests/bin/{Config}/netX.Y → repo root → dotnet-backend
        var appsettingsPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..", "..",
            "dotnet-backend", "src", "CureFlow.Api", "appsettings.json"));

        if (!File.Exists(appsettingsPath))
            return null;

        try
        {
            var json = System.Text.Json.JsonDocument.Parse(File.ReadAllText(appsettingsPath));
            var cs = json.RootElement
                .GetProperty("ConnectionStrings")
                .GetProperty("Default")
                .GetString();

            if (string.IsNullOrWhiteSpace(cs) || cs.Contains("REPLACE-WITH", StringComparison.OrdinalIgnoreCase))
                return null;

            return cs;
        }
        catch
        {
            return null;
        }
    }
}
