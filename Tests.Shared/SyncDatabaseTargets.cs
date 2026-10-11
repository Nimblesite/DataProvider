using Microsoft.Data.SqlClient;
using DatabaseSetupOk = Outcome.Result<bool, Nimblesite.Sync.Core.SyncError>.Ok<
    bool,
    Nimblesite.Sync.Core.SyncError
>;
using PostgresSetup = Nimblesite.Sync.Postgres.PostgresSyncSchema;
using PostgresTriggers = Nimblesite.Sync.Postgres.PostgresTriggerGenerator;
using SetupLogger = Microsoft.Extensions.Logging.Abstractions.NullLogger;
using SetupSqliteConnection = Microsoft.Data.Sqlite.SqliteConnection;
using SqliteSetup = Nimblesite.Sync.SQLite.SyncSchema;
using SqliteTriggers = Nimblesite.Sync.SQLite.TriggerGenerator;

namespace Nimblesite.TestSupport;

// Implements [SYNC-HTTP-PROVIDERS] and [SYNC-CLIENT-SHARED-STORAGE]. One live database setup for shared contracts.
internal static class SyncDatabaseTargets
{
    internal static async Task WithTargetAsync(
        string provider,
        PostgresContainerFixture postgres,
        SqlServerContainerFixture sqlServer,
        Func<string, Task> run,
        bool tableChecks = false
    )
    {
        var target = await CreateAsync(
                provider: provider,
                postgres: postgres,
                sqlServer: sqlServer,
                tableChecks: tableChecks
            )
            .ConfigureAwait(true);
        try
        {
            await run(target.ConnectionString).ConfigureAwait(true);
        }
        finally
        {
            if (target.FilePath is not null)
                File.Delete(target.FilePath);
        }
    }

    internal static async Task<(string ConnectionString, string? FilePath)> CreateAsync(
        string provider,
        PostgresContainerFixture postgres,
        SqlServerContainerFixture sqlServer,
        bool tableChecks = false
    )
    {
        if (provider == "postgres")
        {
            using var connection = await postgres
                .CreateDatabaseAsync("sync_http")
                .ConfigureAwait(false);
            CreatePersonTable("postgres", connection.ConnectionString, "public", tableChecks);
            Xunit.Assert.IsType<DatabaseSetupOk>(PostgresSetup.CreateSchema(connection));
            Xunit.Assert.IsType<DatabaseSetupOk>(
                PostgresSetup.SetOriginId(connection, "server-origin")
            );
            Xunit.Assert.IsType<DatabaseSetupOk>(
                PostgresTriggers.CreateTriggers(connection, "person", SetupLogger.Instance)
            );
            return (connection.ConnectionString, FilePath: null);
        }
        if (provider == "sqlserver")
        {
            var connectionString = await sqlServer
                .CreateDatabaseConnectionStringAsync()
                .ConfigureAwait(false);
            CreatePersonTable("sqlserver", connectionString, "dbo", tableChecks);
            using var serverConnection = new SqlConnection(connectionString);
            await serverConnection.OpenAsync().ConfigureAwait(false);
            Xunit.Assert.IsType<DatabaseSetupOk>(
                Nimblesite.Sync.SqlServer.SqlServerSyncSchema.CreateSchema(serverConnection)
            );
            Xunit.Assert.IsType<DatabaseSetupOk>(
                Nimblesite.Sync.SqlServer.SqlServerSyncStore.SetOriginId(
                    serverConnection,
                    "server-origin"
                )
            );
            Xunit.Assert.IsType<DatabaseSetupOk>(
                Nimblesite.Sync.SqlServer.SqlServerTriggerGenerator.CreateTriggers(
                    serverConnection,
                    "person",
                    SetupLogger.Instance
                )
            );
            return (connectionString, FilePath: null);
        }
        Xunit.Assert.Equal("sqlite", provider);
        var path = Path.Combine(Path.GetTempPath(), $"sync_http_{Guid.NewGuid():N}.db");
        var sqliteConnectionString = $"Data Source={path};Pooling=False";
        CreatePersonTable("sqlite", path, "main", tableChecks);
        using var sqlite = new SetupSqliteConnection(sqliteConnectionString);
        await sqlite.OpenAsync().ConfigureAwait(false);
        Xunit.Assert.IsType<DatabaseSetupOk>(SqliteSetup.CreateSchema(sqlite));
        Xunit.Assert.IsType<DatabaseSetupOk>(SqliteSetup.SetOriginId(sqlite, "server-origin"));
        Xunit.Assert.IsType<DatabaseSetupOk>(
            SqliteTriggers.CreateTriggers(sqlite, "person", SetupLogger.Instance)
        );
        return (sqliteConnectionString, path);
    }

    private static void CreatePersonTable(
        string provider,
        string output,
        string schema,
        bool tableChecks
    )
    {
        var yaml = $$"""
            name: sync_http_person
            tables:
              - name: person
                schema: {{schema}}
                columns:
                  - name: id
                    type: Uuid
                    isNullable: false
                  - name: name
                    type: Text
                    isNullable: false
                    {{(tableChecks ? "" : "checkConstraint: \"name <> ''\"")}}
                  - name: email
                    type: Text
                    isNullable: false
                  - name: manager_id
                    type: Uuid
                    isNullable: true
                primaryKey:
                  columns:
                    - id
                indexes:
                  - name: IX_person_manager
                    columns: [manager_id]
                foreignKeys:
                  - columns: [manager_id]
                    referencedTable: person
                    referencedSchema: {{schema}}
                    referencedColumns: [id]
                    onDelete: NoAction
                    onUpdate: NoAction
            """;
        if (tableChecks)
            yaml +=
                "\n    checkConstraints:\n      - name: CK_person_name\n        expression: \"name <> '' AND email <> ''\"\n";
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, yaml);
            Xunit.Assert.Equal(
                0,
                DataProviderMigrate.Program.Main([
                    "migrate",
                    "--schema",
                    path,
                    "--provider",
                    provider,
                    "--output",
                    output,
                ])
            );
        }
        finally
        {
            File.Delete(path);
        }
    }
}
