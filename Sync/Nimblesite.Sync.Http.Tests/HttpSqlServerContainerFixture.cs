using Microsoft.Data.SqlClient;
using Testcontainers.MsSql;

namespace Nimblesite.Sync.Http.Tests;

public sealed record HttpSqlServerContainerFixture : IAsyncLifetime
{
    private readonly Lazy<Task<MsSqlContainer>> _container = new(StartContainerAsync);
    private long _databaseCounter;

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        if (_container.IsValueCreated && _container.Value.IsCompletedSuccessfully)
        {
            var container = await _container.Value.ConfigureAwait(false);
            await container.DisposeAsync().ConfigureAwait(false);
        }
    }

    public async Task<string> CreateDatabaseConnectionStringAsync()
    {
        var container = await _container.Value.ConfigureAwait(false);
        var name = $"sync_http_{Interlocked.Increment(ref _databaseCounter)}";
        using var admin = new SqlConnection(container.GetConnectionString());
        await admin.OpenAsync().ConfigureAwait(false);
        using var command = admin.CreateCommand();
        command.CommandText = $"CREATE DATABASE [{name}]";
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
        return new SqlConnectionStringBuilder(container.GetConnectionString())
        {
            InitialCatalog = name,
            Pooling = false,
        }.ConnectionString;
    }

    private static async Task<MsSqlContainer> StartContainerAsync()
    {
        var container = new MsSqlBuilder().Build();
        await container.StartAsync().ConfigureAwait(false);
        return container;
    }
}
