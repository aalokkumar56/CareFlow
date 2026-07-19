using Xunit;

namespace CureFlow.UnitTests.Helpers;

/// <summary>Serializes DB integration tests that mutate shared PostgreSQL state.</summary>
[CollectionDefinition("DatabaseIntegration", DisableParallelization = true)]
public class DatabaseIntegrationCollection;
