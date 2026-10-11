namespace Nimblesite.DataProvider.Migration.Tests;

[CollectionDefinition(Name)]
public sealed record MigrationPlatformSuite
    : ICollectionFixture<MigrationPostgresContainerFixture>,
        ICollectionFixture<SqlServerContainerFixture>
{
    public const string Name = "Migration platforms";
}
