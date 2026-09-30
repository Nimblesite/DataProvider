using Microsoft.Data.SqlClient;
using Testcontainers.MsSql;

namespace Nimblesite.DataProvider.Migration.Tests;

[CollectionDefinition(Name)]
public sealed record MigrationPlatformSuite
    : ICollectionFixture<MigrationPostgresContainerFixture>,
        ICollectionFixture<SqlServerContainerFixture>
{
    public const string Name = "Migration platforms";
}

public sealed record SqlServerContainerFixture : IAsyncLifetime
{
    private readonly Lazy<Task<MsSqlContainer>> _container = new(StartContainerAsync);
    private long _databaseCounter;

    // Implements [MIG-TEST-CROSS-PLATFORM]: other providers run before SQL Server starts.
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
        var name = $"migration_regression_{Interlocked.Increment(ref _databaseCounter)}";
        using var admin = new SqlConnection(container.GetConnectionString());
        await admin.OpenAsync().ConfigureAwait(false);
        using var command = admin.CreateCommand();
        command.CommandText = $"CREATE DATABASE [{name}]";
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
        var builder = new SqlConnectionStringBuilder(container.GetConnectionString())
        {
            InitialCatalog = name,
            Pooling = false,
        };
        return builder.ConnectionString;
    }

    private static async Task<MsSqlContainer> StartContainerAsync()
    {
        var container = new MsSqlBuilder().Build();
        await container.StartAsync().ConfigureAwait(false);
        return container;
    }
}
