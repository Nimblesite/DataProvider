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

namespace Nimblesite.Sync.Tests;

// Implements [SYNC-CLIENT-SHARED-STORAGE]. Identical persistence assertions on all three databases.
[Collection(PostgresTestSuite.Name)]
public sealed partial record SyncClientStoragePlatformTests
    : IClassFixture<SqlServerContainerFixture>
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
            global::Tests
                .Shared.ResultAssertions.ExactOk(
                    actual: SyncClientStorage.GetByOrigin<DbException>(
                        connection: connection,
                        originId: "missing"
                    )
                )
                .Value
        );
        var first = Dataset_AssertRoundTrips_1();
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
            global::Tests
                .Shared.ResultAssertions.ExactOk(
                    actual: SyncClientStorage.GetByOrigin<DbException>(
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
        global::Tests.Shared.ResultAssertions.ExactOk(
            actual: SyncClientStorage.Delete<DbException>(
                connection: connection,
                originId: "second"
            )
        );
        Assert.Single(Clients(connection: connection));
        global::Tests.Shared.ResultAssertions.EqualValue(
            expected: 1,
            actual: SyncClientStorage.DeleteMultiple<DbException>(
                connection: connection,
                originIds: ["O'Brien 雪", "O'Brien 雪", "missing"],
                failurePrefix: "Delete failed"
            )
        );
        Assert.Empty(Clients(connection: connection));
        global::Tests.Shared.ResultAssertions.EqualValue(
            expected: 0,
            actual: SyncClientStorage.DeleteMultiple<DbException>(
                connection: connection,
                originIds: [],
                failurePrefix: "Delete failed"
            )
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
        global::Tests.Shared.ResultAssertions.ExactOk(
            actual: connection is SqlConnection server
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
