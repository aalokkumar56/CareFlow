using Xunit;

namespace CureFlow.IntegrationTests;

/// <summary>Serializes DB integration tests that mutate shared PostgreSQL state.</summary>
[CollectionDefinition("Database", DisableParallelization = true)]
public class DatabaseCollection;
