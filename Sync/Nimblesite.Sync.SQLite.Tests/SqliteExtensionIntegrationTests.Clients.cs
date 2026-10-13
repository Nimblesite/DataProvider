using Nimblesite.DataProvider.Migration.Core;
using Nimblesite.DataProvider.Migration.SQLite;
using static global::Tests.Shared.ResultAssertions;
using static global::Tests.Shared.SyncFixtureSqliteHelpers;
using ClientFailure = Outcome.Result<
    Nimblesite.Sync.Core.SyncClient?,
    Nimblesite.Sync.Core.SyncError
>.Error<Nimblesite.Sync.Core.SyncClient?, Nimblesite.Sync.Core.SyncError>;
using ClientListFailure = Outcome.Result<
    System.Collections.Generic.IReadOnlyList<Nimblesite.Sync.Core.SyncClient>,
    Nimblesite.Sync.Core.SyncError
>.Error<
    System.Collections.Generic.IReadOnlyList<Nimblesite.Sync.Core.SyncClient>,
    Nimblesite.Sync.Core.SyncError
>;
using IntFailure = Outcome.Result<int, Nimblesite.Sync.Core.SyncError>.Error<
    int,
    Nimblesite.Sync.Core.SyncError
>;
using LongFailure = Outcome.Result<long, Nimblesite.Sync.Core.SyncError>.Error<
    long,
    Nimblesite.Sync.Core.SyncError
>;

namespace Nimblesite.Sync.SQLite.Tests;

// Implements [SYNC-CLIENT-API-SHARED].
public sealed partial class SqliteExtensionIntegrationTests
{
    #region Client Tracking Extension Methods

    [Fact]
    public void GetAllSyncClients_EmptyDatabase_ReturnsEmptyList()
    {
        // Act
        var result = _db.GetAllSyncClients();

        // Assert
        EmptyList(actual: result);
    }

    [Fact]
    public void UpsertSyncClient_NewClient_Inserts()
    {
        // Arrange
        var client = CreateClient();

        // Act
        global::Tests.Shared.ResultAssertions.Ok(actual: _db.UpsertSyncClient(client));

        var allClients = _db.GetAllSyncClients();
        Assert.Single((Ok(actual: allClients)).Value);
    }

    [Fact]
    public void UpsertSyncClient_ExistingClient_Updates()
    {
        // Arrange
        var client1 = new SyncClient("client-1", 100, Timestamp, Timestamp);
        _db.UpsertSyncClient(client1);

        var client2 = new SyncClient("client-1", 200, "2025-02-01T00:00:00Z", Timestamp);

        // Act
        global::Tests.Shared.ResultAssertions.Ok(actual: _db.UpsertSyncClient(client2));

        var clients = (Ok(read: () => _db.GetAllSyncClients())).Value;
        Assert.Single(clients);
        Assert.Equal(200, clients[0].LastSyncVersion);
    }

    [Fact]
    public void GetAllSyncClients_MultipleClients_ReturnsOrderedByVersion()
    {
        // Arrange
        _db.UpsertSyncClient(new SyncClient("client-a", 300, Timestamp, Timestamp));
        _db.UpsertSyncClient(new SyncClient("client-b", 100, Timestamp, Timestamp));
        _db.UpsertSyncClient(new SyncClient("client-c", 200, Timestamp, Timestamp));

        // Act
        // Act
        // Assert
        var clients = Ok(read: () => _db.GetAllSyncClients()).Value;
        Assert.Equal(3, clients.Count);
        Assert.Equal("client-b", clients[0].OriginId); // Lowest version first
        Assert.Equal("client-c", clients[1].OriginId);
        Assert.Equal("client-a", clients[2].OriginId);
    }

    [Fact]
    public void DeleteStaleSyncClients_RemovesSpecifiedClients()
    {
        // Arrange
        _db.UpsertSyncClient(new SyncClient("client-1", 100, Timestamp, Timestamp));
        _db.UpsertSyncClient(new SyncClient("client-2", 200, Timestamp, Timestamp));
        _db.UpsertSyncClient(new SyncClient("client-3", 300, Timestamp, Timestamp));

        // Act
        var result = _db.DeleteStaleSyncClients(["client-1", "client-2"]);

        // Assert
        global::Nimblesite.Sync.SQLite.Tests.SyncRepositoryIntegrationTests.VerifyReusableWorkflow7(
            input0: result,
            value0: 2
        );

        var clients = (Ok(read: () => _db.GetAllSyncClients())).Value;
        Assert.Single(clients);
        Assert.Equal("client-3", clients[0].OriginId);
    }

    #endregion

    [Theory]
    [MemberData(nameof(ClientApi_MissingTableRetainsDatabaseErrorInlineCases))]
    public void ClientApi_MissingTableRetainsDatabaseError(string api, string expectedPrefix)
    {
        var drop = MigrationRunner.Apply(
            connection: _db,
            operations: [new DropTableOperation(Schema: "", TableName: "_sync_clients")],
            generateDdl: SqliteDdlGenerator.Generate,
            options: MigrationOptions.Destructive
        );
        global::Tests.Shared.ResultAssertions.ExactOk(actual: drop);
        var error = Assert.IsType<SyncErrorDatabase>(ReadClientError(InvokeClientApi(api: api)));
        Assert.StartsWith(expectedPrefix, error.Message, StringComparison.Ordinal);
    }

    private object InvokeClientApi(string api) =>
        api switch
        {
            "extension-list" => _db.GetAllSyncClients(),
            "repository-list" => SyncClientRepository.GetAll(connection: _db),
            "extension-upsert" => _db.UpsertSyncClient(client: CreateClient()),
            "repository-upsert" => SyncClientRepository.Upsert(
                connection: _db,
                client: CreateClient()
            ),
            "extension-delete" => _db.DeleteStaleSyncClients(originIds: ["client-1"]),
            "repository-delete" => SyncClientRepository.DeleteMultiple(
                connection: _db,
                originIds: ["client-1"]
            ),
            "repository-origin" => SyncClientRepository.GetByOrigin(
                connection: _db,
                originId: "client-1"
            ),
            "repository-minimum" => SyncClientRepository.GetMinVersion(connection: _db),
            "repository-delete-one" => SyncClientRepository.Delete(
                connection: _db,
                originId: "client-1"
            ),
            _ => api,
        };

    private static SyncError? ReadClientError(object result) =>
        result switch
        {
            ClientFailure failure => failure.Value,
            ClientListFailure failure => failure.Value,
            BoolSyncError failure => failure.Value,
            IntFailure failure => failure.Value,
            LongFailure failure => failure.Value,
            _ => null,
        };

    private static SyncClient CreateClient() =>
        new(
            OriginId: "client-1",
            LastSyncVersion: 100,
            LastSyncTimestamp: Timestamp,
            CreatedAt: Timestamp
        );
}
