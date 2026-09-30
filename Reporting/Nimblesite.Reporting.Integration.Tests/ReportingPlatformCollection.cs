using Nimblesite.DataProvider.Migration.Tests;

namespace Nimblesite.Reporting.Integration.Tests;

// Implements [MIG-TEST-CROSS-PLATFORM]: container startup is lazy per provider.
[CollectionDefinition(Name)]
public sealed record ReportingPlatformCollection
    : ICollectionFixture<MigrationPostgresContainerFixture>,
        ICollectionFixture<SqlServerContainerFixture>
{
    public const string Name = "Reporting platforms";
}
