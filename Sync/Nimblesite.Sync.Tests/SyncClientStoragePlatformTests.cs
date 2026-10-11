using System.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Nimblesite.TestSupport;
using Npgsql;
using ClientListOk = Outcome.Result<
    System.Collections.Generic.IReadOnlyList<Nimblesite.Sync.Core.SyncClient>,
    Nimblesite.Sync.Core.SyncError
>.Ok<
    System.Collections.Generic.IReadOnlyList<Nimblesite.Sync.Core.SyncClient>,
    Nimblesite.Sync.Core.SyncError
>;
using ClientOk = Outcome.Result<
    Nimblesite.Sync.Core.SyncClient?,
    Nimblesite.Sync.Core.SyncError
>.Ok<Nimblesite.Sync.Core.SyncClient?, Nimblesite.Sync.Core.SyncError>;

namespace Nimblesite.Sync.Tests;

// Implements [SYNC-CLIENT-SHARED-STORAGE]. Identical persistence assertions on all three databases.
[Collection(PostgresTestSuite.Name)]
public sealed record SyncClientStoragePlatformTests : IClassFixture<SqlServerContainerFixture>
{
    private readonly PostgresContainerFixture _postgres;
    private readonly SqlServerContainerFixture _sqlServer;

    public SyncClientStoragePlatformTests(
        PostgresContainerFixture postgres,
        SqlServerContainerFixture sqlServer
    )
    {
        _postgres = postgres;
        _sqlServer = sqlServer;
    }

    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgres")]
    [InlineData("sqlserver")]
    public Task ClientStorage_RoundTripsOrdersUpdatesAndDeletes(string provider) =>
        SyncDatabaseTargets.WithTargetAsync(
            provider: provider,
            postgres: _postgres,
            sqlServer: _sqlServer,
            run: async value =>
            {
                using DbConnection connection = provider switch
                {
                    "sqlite" => new SqliteConnection(value),
                    "postgres" => new NpgsqlConnection(value),
                    _ => new SqlConnection(value),
                };
                await connection.OpenAsync().ConfigureAwait(true);
                AssertRoundTrips(connection: connection);
                AssertDeletes(connection: connection);
            }
        );

    private static void AssertRoundTrips(DbConnection connection)
    {
        Assert.Empty(Clients(connection: connection));
        Assert.Null(
            Assert
                .IsType<ClientOk>(
                    SyncClientStorage.GetByOrigin<DbException>(
                        connection: connection,
                        originId: "missing"
                    )
                )
                .Value
        );
        var first = new SyncClient(
            OriginId: "O'Brien 雪",
            LastSyncVersion: 2,
            LastSyncTimestamp: "2024-01-01T00:00:00Z",
            CreatedAt: "2024-01-01T00:00:00Z"
        );
        var second = first with { OriginId = "second", LastSyncVersion = 7 };
        Upsert(connection: connection, client: first);
        Upsert(connection: connection, client: second);
        Assert.Equal([first, second], Clients(connection: connection));
        var updated = first with
        {
            LastSyncVersion = 9,
            LastSyncTimestamp = "2024-02-01T00:00:00Z",
        };
        Upsert(connection: connection, client: updated with { CreatedAt = "2024-03-01T00:00:00Z" });
        Assert.Equal(
            updated,
            Assert
                .IsType<ClientOk>(
                    SyncClientStorage.GetByOrigin<DbException>(
                        connection: connection,
                        originId: first.OriginId
                    )
                )
                .Value
        );
        Assert.Equal([second, updated], Clients(connection: connection));
    }

    private static void AssertDeletes(DbConnection connection)
    {
        Assert.IsType<BoolSyncOk>(
            SyncClientStorage.Delete<DbException>(connection: connection, originId: "second")
        );
        Assert.Single(Clients(connection: connection));
        Assert.Equal(
            1,
            Assert
                .IsType<IntSyncOk>(
                    SyncClientStorage.DeleteMultiple<DbException>(
                        connection: connection,
                        originIds: ["O'Brien 雪", "O'Brien 雪", "missing"],
                        failurePrefix: "Delete failed"
                    )
                )
                .Value
        );
        Assert.Empty(Clients(connection: connection));
        Assert.Equal(
            0,
            Assert
                .IsType<IntSyncOk>(
                    SyncClientStorage.DeleteMultiple<DbException>(
                        connection: connection,
                        originIds: [],
                        failurePrefix: "Delete failed"
                    )
                )
                .Value
        );
    }

    private static IReadOnlyList<SyncClient> Clients(DbConnection connection) =>
        Assert
            .IsType<ClientListOk>(
                SyncClientStorage.GetAll<DbException>(
                    connection: connection,
                    failurePrefix: "Read failed"
                )
            )
            .Value;

    private static void Upsert(DbConnection connection, SyncClient client) =>
        Assert.IsType<BoolSyncOk>(
            connection is SqlConnection server
                ? Nimblesite.Sync.SqlServer.SqlServerSyncStore.UpsertClient(
                    connection: server,
                    client: client
                )
                : SyncClientStorage.Upsert<DbException>(
                    connection: connection,
                    client: client,
                    failurePrefix: "Upsert failed"
                )
        );
}
