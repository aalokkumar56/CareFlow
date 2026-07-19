using FluentAssertions;
using Xunit;

namespace CureFlow.IntegrationTests;

/// <summary>
/// Stub for PostgreSQL RLS isolation (full suite to migrate from unit <c>TenantRlsIsolationTests</c>).
/// Skips when <c>CUREFLOW_TEST_CONNECTION</c> is unset.
/// </summary>
[Collection("Database")]
public class TenantRlsIntegrationTests
{
    [SkippableFact]
    public void Rls_Suite_Skips_Without_CureFlowTestConnection()
    {
        var connectionString = Environment.GetEnvironmentVariable("CUREFLOW_TEST_CONNECTION");
        Skip.If(string.IsNullOrWhiteSpace(connectionString), "CUREFLOW_TEST_CONNECTION is not set.");

        // Placeholder until TenantRlsIsolationTests is migrated here
        connectionString.Should().NotBeNullOrWhiteSpace();
    }
}
