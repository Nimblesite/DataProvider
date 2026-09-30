namespace Nimblesite.DataProvider.Migration.Tests;

public sealed record MigrationPostgresContainerFixture : IAsyncLifetime
{
    private readonly Lazy<Task<PostgresContainerFixture>> _fixture = new(StartFixtureAsync);

    // Implements [MIG-TEST-CROSS-PLATFORM]: a Postgres outage cannot block other providers.
    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        if (_fixture.IsValueCreated && _fixture.Value.IsCompletedSuccessfully)
        {
            var fixture = await _fixture.Value.ConfigureAwait(false);
            await fixture.DisposeAsync().ConfigureAwait(false);
        }
    }

    public async Task<NpgsqlConnection> CreateDatabaseAsync(string namePrefix)
    {
        var fixture = await _fixture.Value.ConfigureAwait(false);
        return await fixture.CreateDatabaseAsync(namePrefix).ConfigureAwait(false);
    }

    private static async Task<PostgresContainerFixture> StartFixtureAsync()
    {
        var fixture = new PostgresContainerFixture();
        await fixture.InitializeAsync().ConfigureAwait(false);
        return fixture;
    }
}
