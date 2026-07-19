using Xunit;

namespace CureFlow.UnitTests;

/// <summary>Serializes DB integration tests that mutate shared PostgreSQL state.</summary>
[CollectionDefinition("DatabaseIntegration", DisableParallelization = true)]
public class DatabaseIntegrationCollection;
