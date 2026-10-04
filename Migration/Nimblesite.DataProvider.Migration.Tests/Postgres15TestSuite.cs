namespace Nimblesite.DataProvider.Migration.Tests;

[CollectionDefinition(Name)]
public sealed class Postgres15TestSuite : ICollectionFixture<Postgres15ContainerFixture>
{
    public const string Name = "Postgres15";
}
