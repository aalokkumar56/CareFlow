using System.Net;
using FluentAssertions;
using Xunit;

namespace CureFlow.IntegrationTests;

public class HealthEndpointTests : IntegrationTestBase
{
    public HealthEndpointTests(CustomWebApplicationFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task Get_api_health_returns_ok()
    {
        var result = await Client.GetAsync("/api/health");

        result.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
