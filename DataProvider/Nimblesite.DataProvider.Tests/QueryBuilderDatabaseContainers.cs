using Nimblesite.TestSupport;
using Testcontainers.PostgreSql;

namespace Nimblesite.DataProvider.Tests;

public sealed record QueryBuilderDatabaseContainers : IAsyncLifetime
{
    private readonly Lazy<Task<PostgreSqlContainer>> _postgres = new(StartPostgresAsync);
    private readonly SqlServerContainerFixture _sqlServer = new();

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        if (_postgres.IsValueCreated && _postgres.Value.IsCompletedSuccessfully)
        {
            var postgres = await _postgres.Value.ConfigureAwait(false);
            await postgres.DisposeAsync().ConfigureAwait(false);
        }
        await _sqlServer.DisposeAsync().ConfigureAwait(false);
    }

    public async Task<string> GetConnectionStringAsync(string provider) =>
        provider == "postgres"
            ? new Npgsql.NpgsqlConnectionStringBuilder(
                (await _postgres.Value.ConfigureAwait(false)).GetConnectionString()
            )
            {
                PersistSecurityInfo = true,
            }.ConnectionString
            : await _sqlServer.CreateDatabaseConnectionStringAsync().ConfigureAwait(false);

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
}
