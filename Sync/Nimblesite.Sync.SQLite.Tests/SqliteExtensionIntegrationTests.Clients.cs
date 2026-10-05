using Nimblesite.DataProvider.Migration.Core;
using Nimblesite.DataProvider.Migration.SQLite;
using Xunit;
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
using MigrationOk = Outcome.Result<bool, Nimblesite.DataProvider.Migration.Core.MigrationError>.Ok<
    bool,
    Nimblesite.DataProvider.Migration.Core.MigrationError
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
        Assert.True(result is SyncClientListOk);
        Assert.Empty(((SyncClientListOk)result).Value);
    }

    [Fact]
    public void UpsertSyncClient_NewClient_Inserts()
    {
        // Arrange
        var client = CreateClient();

        // Act
        var result = _db.UpsertSyncClient(client);

        // Assert
        Assert.True(result is BoolSyncOk);

        var allClients = _db.GetAllSyncClients();
        Assert.Single(((SyncClientListOk)allClients).Value);
    }

    [Fact]
    public void UpsertSyncClient_ExistingClient_Updates()
    {
        // Arrange
        var client1 = new SyncClient("client-1", 100, Timestamp, Timestamp);
        _db.UpsertSyncClient(client1);

        var client2 = new SyncClient("client-1", 200, "2025-02-01T00:00:00Z", Timestamp);

        // Act
        var result = _db.UpsertSyncClient(client2);

        // Assert
        Assert.True(result is BoolSyncOk);

        var allClients = _db.GetAllSyncClients();
        var clients = ((SyncClientListOk)allClients).Value;
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
        var result = _db.GetAllSyncClients();

        // Assert
        Assert.True(result is SyncClientListOk);
        var clients = ((SyncClientListOk)result).Value;
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
        Assert.True(result is IntSyncOk);
        Assert.Equal(2, ((IntSyncOk)result).Value);

        var remaining = _db.GetAllSyncClients();
        var clients = ((SyncClientListOk)remaining).Value;
        Assert.Single(clients);
        Assert.Equal("client-3", clients[0].OriginId);
    }

    #endregion

    [Theory]
    [InlineData("extension-list", "Failed to get clients:")]
    [InlineData("repository-list", "Failed to get sync clients:")]
    [InlineData("extension-upsert", "Failed to upsert client:")]
    [InlineData("repository-upsert", "Failed to upsert sync client:")]
    [InlineData("extension-delete", "Failed to delete stale clients:")]
    [InlineData("repository-delete", "Failed to delete sync clients:")]
    [InlineData("repository-origin", "Failed to get sync client:")]
    [InlineData("repository-minimum", "Failed to get minimum sync version:")]
    [InlineData("repository-delete-one", "Failed to delete sync client:")]
    public void ClientApi_MissingTableRetainsDatabaseError(string api, string expectedPrefix)
    {
        var drop = MigrationRunner.Apply(
            connection: _db,
            operations: [new DropTableOperation(Schema: "", TableName: "_sync_clients")],
            generateDdl: SqliteDdlGenerator.Generate,
            options: MigrationOptions.Destructive
        );
        Assert.IsType<MigrationOk>(drop);
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
