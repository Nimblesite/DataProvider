using Testcontainers.MsSql;
using Testcontainers.PostgreSql;

namespace Nimblesite.DataProvider.Tests;

public sealed record QueryBuilderDatabaseContainers : IAsyncLifetime
{
    private readonly Lazy<Task<PostgreSqlContainer>> _postgres = new(StartPostgresAsync);
    private readonly Lazy<Task<MsSqlContainer>> _sqlServer = new(StartSqlServerAsync);

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        if (_postgres.IsValueCreated && _postgres.Value.IsCompletedSuccessfully)
        {
            var postgres = await _postgres.Value.ConfigureAwait(false);
            await postgres.DisposeAsync().ConfigureAwait(false);
        }
        if (_sqlServer.IsValueCreated && _sqlServer.Value.IsCompletedSuccessfully)
        {
            var sqlServer = await _sqlServer.Value.ConfigureAwait(false);
            await sqlServer.DisposeAsync().ConfigureAwait(false);
        }
    }

    public async Task<string> GetConnectionStringAsync(string provider) =>
        provider == "postgres"
            ? (await _postgres.Value.ConfigureAwait(false)).GetConnectionString()
            : (await _sqlServer.Value.ConfigureAwait(false)).GetConnectionString();

    private static async Task<PostgreSqlContainer> StartPostgresAsync()
    {
        var container = new PostgreSqlBuilder()
            .WithImage("pgvector/pgvector:pg16")
            .WithDatabase("postgres")
            .WithUsername("test")
            .WithPassword("test")
            .Build();
        await container.StartAsync().ConfigureAwait(false);
        return container;
    }

    private static async Task<MsSqlContainer> StartSqlServerAsync()
    {
        var container = new MsSqlBuilder().Build();
        await container.StartAsync().ConfigureAwait(false);
        return container;
    }
}
