namespace Nimblesite.DataProvider.Migration.Tests;

// Implements [RLS-DIFF]: PostgreSQL 15 deparses view and policy columns differently.
public sealed record Postgres15ContainerFixture : IAsyncLifetime
{
    private readonly PostgresContainerFixture _fixture = new();

    public Task InitializeAsync() => _fixture.InitializeAsync(image: "postgres:15.1");

    public Task DisposeAsync() => _fixture.DisposeAsync();

    public NpgsqlConnection CreateDatabase(string namePrefix) =>
        _fixture.CreateDatabase(namePrefix: namePrefix);
}
