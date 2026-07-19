using Xunit;

namespace CureFlow.IntegrationTests;

/// <summary>
/// Optional base for suites that share <see cref="CustomWebApplicationFactory"/>.
/// Prefer <c>IClassFixture&lt;CustomWebApplicationFactory&gt;</c> for new files.
/// </summary>
public abstract class IntegrationTestBase : IClassFixture<CustomWebApplicationFactory>
{
    protected CustomWebApplicationFactory Factory { get; }
    protected HttpClient Client { get; }

    protected IntegrationTestBase(CustomWebApplicationFactory factory)
    {
        Factory = factory;
        Client = factory.CreateClient();
    }
}
