using Microsoft.Data.SqlClient;
using Testcontainers.MsSql;

namespace Nimblesite.TestSupport;

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
        global::Tests.Shared.DatabaseCommands.Execute(
            connection: admin,
            configure: command =>
                command.CommandText =
                    "DECLARE @sql nvarchar(max) = N'CREATE DATABASE ' + QUOTENAME(@database); EXEC sys.sp_executesql @sql;",
            parameters: () => [("@database", name)]
        );
        var builder = new SqlConnectionStringBuilder(container.GetConnectionString())
        {
            InitialCatalog = name,
            Pooling = false,
            PersistSecurityInfo = true,
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
