using Xunit;

namespace Dip.Api.IntegrationTests;

// One DipApiFactory shared across every integration test class in the assembly.
// Two reasons:
//   1. Serilog's static Log.Logger can only be initialised once per process;
//      each fresh WebApplicationFactory retries and fails ("already frozen").
//   2. Reduces DB schema churn — one CREATE/DROP SCHEMA per test run instead
//      of once per class.
[CollectionDefinition(Name)]
public sealed class IntegrationTestCollection : ICollectionFixture<DipApiFactory>
{
    public const string Name = "Integration";
}
