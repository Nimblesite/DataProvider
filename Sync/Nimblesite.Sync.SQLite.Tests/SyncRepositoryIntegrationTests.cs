// Implements [CI-TEST-SHARED-COMMAND].
using Microsoft.Data.Sqlite;
using Tests.Shared;
using Xunit;

namespace Nimblesite.Sync.SQLite.Tests;

/// <summary>
/// Integration tests for SQLite repository classes.
/// Tests SyncClientRepository, SyncLogRepository, and SubscriptionRepository.
/// NO MOCKS - real SQLite databases only!
/// </summary>
public sealed partial class SyncRepositoryIntegrationTests : IDisposable
{
    private readonly SqliteConnection _db;
    private readonly string _dbPath = Path.Combine(
        Path.GetTempPath(),
        $"syncrepositoryintegrationtests_{Guid.NewGuid()}.db"
    );
    private readonly string _originId = Guid.NewGuid().ToString();
    private const string Timestamp = "2025-01-01T00:00:00.000Z";

    public SyncRepositoryIntegrationTests()
    {
        _db = new SqliteConnection($"Data Source={_dbPath}");
        _db.Open();
        SyncSchema.CreateSchema(_db);
        SyncSchema.SetOriginId(_db, _originId);

        // Create test table with triggers
        DatabaseCommands.Execute(
            connection: _db,
            configure: command =>
                command.CommandText = Data_Shared_1
        );
        TriggerGenerator.CreateTriggers(_db, "Person", NullLogger.Instance);
    }

    #region SyncClientRepository Tests

    [Fact]
    public void SyncClientRepository_GetAll_EmptyDatabase_ReturnsEmptyList()
    {
        // Act
        var result = SyncClientRepository.GetAll(_db);

        // Assert
        Assert.True(result is SyncClientListOk);
        Assert.Empty(((SyncClientListOk)result).Value);
    }

    [Fact]
    public void SyncClientRepository_Upsert_NewClient_Inserts()
    {
        // Arrange
        var client = new SyncClient("client-1", 100, Timestamp, Timestamp);

        // Act
        var result = SyncClientRepository.Upsert(_db, client);

        // Assert
        Assert.True(result is BoolSyncOk);

        var allClients = SyncClientRepository.GetAll(_db);
        Assert.Single(((SyncClientListOk)allClients).Value);
    }

    [Fact]
    public void SyncClientRepository_Upsert_ExistingClient_Updates()
    {
        // Arrange
        var client1 = new SyncClient("client-1", 100, Timestamp, Timestamp);
        SyncClientRepository.Upsert(_db, client1);

        var client2 = new SyncClient("client-1", 200, "2025-02-01T00:00:00Z", Timestamp);

        // Act
        var result = SyncClientRepository.Upsert(_db, client2);

        // Assert
        Assert.True(result is BoolSyncOk);

        var retrieved = SyncClientRepository.GetByOrigin(_db, "client-1");
        var client = ((SyncClientOk)retrieved).Value;
        Assert.NotNull(client);
        Assert.Equal(200, client.LastSyncVersion);
    }

    [Fact]
    public void SyncClientRepository_GetByOrigin_ExistingClient_ReturnsClient()
    {
        // Arrange
        var client = new SyncClient("client-1", 100, Timestamp, Timestamp);
        SyncClientRepository.Upsert(_db, client);

        // Act
        var result = SyncClientRepository.GetByOrigin(_db, "client-1");

        // Assert
        Assert.True(result is SyncClientOk);
        var retrieved = ((SyncClientOk)result).Value;
        Assert.NotNull(retrieved);
        Assert.Equal("client-1", retrieved.OriginId);
        Assert.Equal(100, retrieved.LastSyncVersion);
    }

    [Fact]
    public void SyncClientRepository_GetByOrigin_NonExistingClient_ReturnsNull()
    {
        // Act
        var result = SyncClientRepository.GetByOrigin(_db, "nonexistent");

        // Assert
        Assert.True(result is SyncClientOk);
        Assert.Null(((SyncClientOk)result).Value);
    }

    [Fact]
    public void SyncClientRepository_Delete_ExistingClient_Removes()
    {
        // Arrange
        SyncClientRepository.Upsert(_db, new SyncClient("client-1", 100, Timestamp, Timestamp));

        // Act
        var result = SyncClientRepository.Delete(_db, "client-1");

        // Assert
        Assert.True(result is BoolSyncOk);

        var retrieved = SyncClientRepository.GetByOrigin(_db, "client-1");
        Assert.Null(((SyncClientOk)retrieved).Value);
    }

    [Fact]
    public void SyncClientRepository_Delete_NonExistingClient_StillSucceeds()
    {
        // Act
        var result = SyncClientRepository.Delete(_db, "nonexistent");

        // Assert
        Assert.True(result is BoolSyncOk);
    }

    [Fact]
    public void SyncClientRepository_GetMinVersion_EmptyDatabase_ReturnsZero()
    {
        // Act
        var result = SyncClientRepository.GetMinVersion(_db);

        // Assert
        Assert.True(result is LongSyncOk);
        Assert.Equal(0, ((LongSyncOk)result).Value);
    }

    [Fact]
    public void SyncClientRepository_GetMinVersion_WithClients_ReturnsMinimum()
    {
        // Arrange
        SyncClientRepository.Upsert(_db, new SyncClient("client-a", 300, Timestamp, Timestamp));
        SyncClientRepository.Upsert(_db, new SyncClient("client-b", 100, Timestamp, Timestamp));
        SyncClientRepository.Upsert(_db, new SyncClient("client-c", 200, Timestamp, Timestamp));

        // Act
        var result = SyncClientRepository.GetMinVersion(_db);

        // Assert
        Assert.True(result is LongSyncOk);
        Assert.Equal(100, ((LongSyncOk)result).Value);
    }

    [Fact]
    public void SyncClientRepository_DeleteMultiple_RemovesSpecified()
    {
        // Arrange
        SyncClientRepository.Upsert(_db, new SyncClient("client-1", 100, Timestamp, Timestamp));
        SyncClientRepository.Upsert(_db, new SyncClient("client-2", 200, Timestamp, Timestamp));
        SyncClientRepository.Upsert(_db, new SyncClient("client-3", 300, Timestamp, Timestamp));

        // Act
        var result = SyncClientRepository.DeleteMultiple(_db, ["client-1", "client-2"]);

        // Assert
        Assert.True(result is IntSyncOk);
        Assert.Equal(2, ((IntSyncOk)result).Value);

        var remaining = SyncClientRepository.GetAll(_db);
        Assert.Single(((SyncClientListOk)remaining).Value);
    }

    #endregion

    #region SyncLogRepository Tests

    [Fact]
    public void SyncLogRepository_FetchChanges_EmptyLog_ReturnsEmptyList()
    {
        // Act
        var result = SyncLogRepository.FetchChanges(_db, 0, 100);

        // Assert
        Assert.True(result is SyncLogListOk);
        Assert.Empty(((SyncLogListOk)result).Value);
    }

    [Fact]
    public void SyncLogRepository_FetchChanges_WithEntries_ReturnsEntries()
    {
        // Arrange - Insert via user table to trigger logging
        DatabaseCommands.Execute(
            connection: _db,
            configure: command =>
                command.CommandText =
                    "INSERT INTO Person (Id, Name, Age) VALUES ('p1', 'Alice', 30)"
        );

        // Act
        var result = SyncLogRepository.FetchChanges(_db, 0, 100);

        // Assert
        Assert.True(result is SyncLogListOk);
        var entries = ((SyncLogListOk)result).Value;
        Assert.Single(entries);
        Assert.Equal("Person", entries[0].TableName);
    }

    [Fact]
    public void SyncLogRepository_FetchChanges_RespectsBatchSize()
    {
        // Arrange
        for (var i = 0; i < 10; i++)
        {
            DatabaseCommands.Execute(
                connection: _db,
                configure: command =>
                    command.CommandText =
                        $"INSERT INTO Person (Id, Name, Age) VALUES ('p{i}', 'Person {i}', {20 + i})"
            );
        }

        // Act
        var result = SyncLogRepository.FetchChanges(_db, 0, 5);

        // Assert
        Assert.True(result is SyncLogListOk);
        Assert.Equal(5, ((SyncLogListOk)result).Value.Count);
    }

    [Fact]
    public void SyncLogRepository_FetchChanges_FromVersion_FiltersCorrectly()
    {
        // Arrange
        for (var i = 0; i < 5; i++)
        {
            DatabaseCommands.Execute(
                connection: _db,
                configure: command =>
                    command.CommandText =
                        $"INSERT INTO Person (Id, Name, Age) VALUES ('p{i}', 'Person {i}', {20 + i})"
            );
        }

        // Get the max version
        var maxResult = SyncLogRepository.GetMaxVersion(_db);
        var maxVersion = ((LongSyncOk)maxResult).Value;

        // Act - Fetch from version 3
        var result = SyncLogRepository.FetchChanges(_db, maxVersion - 2, 100);

        // Assert
        Assert.True(result is SyncLogListOk);
        Assert.Equal(2, ((SyncLogListOk)result).Value.Count);
    }

    [Fact]
    public void SyncLogRepository_GetMaxVersion_EmptyLog_ReturnsZero()
    {
        // Act
        var result = SyncLogRepository.GetMaxVersion(_db);

        // Assert
        Assert.True(result is LongSyncOk);
        Assert.Equal(0, ((LongSyncOk)result).Value);
    }

    [Fact]
    public void SyncLogRepository_GetMaxVersion_WithEntries_ReturnsMax()
    {
        // Arrange
        for (var i = 0; i < 5; i++)
        {
            DatabaseCommands.Execute(
                connection: _db,
                configure: command =>
                    command.CommandText =
                        $"INSERT INTO Person (Id, Name, Age) VALUES ('p{i}', 'Person {i}', {20 + i})"
            );
        }

        // Act
        var result = SyncLogRepository.GetMaxVersion(_db);

        // Assert
        Assert.True(result is LongSyncOk);
        Assert.True(((LongSyncOk)result).Value >= 5);
    }

    [Fact]
    public void SyncLogRepository_Insert_LogsUpdate()
    {
        // Arrange
        DatabaseCommands.Execute(
            connection: _db,
            configure: command =>
                command.CommandText =
                    "INSERT INTO Person (Id, Name, Age) VALUES ('p1', 'Alice', 30)"
        );

        var versionAfterInsert = ((LongSyncOk)SyncLogRepository.GetMaxVersion(_db)).Value;

        // Act - Update
        DatabaseCommands.Execute(
            connection: _db,
            configure: command =>
                command.CommandText = "UPDATE Person SET Name = 'Alice Updated' WHERE Id = 'p1'"
        );

        // Assert
        var result = SyncLogRepository.FetchChanges(_db, versionAfterInsert, 100);
        var entries = ((SyncLogListOk)result).Value;
        Assert.Single(entries);
        Assert.Equal(SyncOperation.Update, entries[0].Operation);
    }

    [Fact]
    public void SyncLogRepository_Delete_LogsTombstone()
    {
        // Arrange
        DatabaseCommands.Execute(
            connection: _db,
            configure: command =>
                command.CommandText =
                    "INSERT INTO Person (Id, Name, Age) VALUES ('p1', 'Alice', 30)"
        );

        var versionAfterInsert = ((LongSyncOk)SyncLogRepository.GetMaxVersion(_db)).Value;

        // Act - Delete
        DatabaseCommands.Execute(
            connection: _db,
            configure: command => command.CommandText = "DELETE FROM Person WHERE Id = 'p1'"
        );

        // Assert
        var result = SyncLogRepository.FetchChanges(_db, versionAfterInsert, 100);
        var entries = ((SyncLogListOk)result).Value;
        Assert.Single(entries);
        Assert.Equal(SyncOperation.Delete, entries[0].Operation);
        Assert.Null(entries[0].Payload); // Tombstone has no payload
    }

    [Fact]
    public void SyncLogRepository_GetLastServerVersion_Default_ReturnsZero()
    {
        // Act
        var result = SyncLogRepository.GetLastServerVersion(_db);

        // Assert
        Assert.True(result is LongSyncOk);
        Assert.Equal(0, ((LongSyncOk)result).Value);
    }

    [Fact]
    public void SyncLogRepository_UpdateLastServerVersion_UpdatesValue()
    {
        // Act
        var updateResult = SyncLogRepository.UpdateLastServerVersion(_db, 100);

        // Assert
        Assert.True(updateResult is BoolSyncOk);

        var getResult = SyncLogRepository.GetLastServerVersion(_db);
        Assert.True(getResult is LongSyncOk);
        Assert.Equal(100, ((LongSyncOk)getResult).Value);
    }

    [Fact]
    public void SyncLogRepository_GetMinVersion_EmptyLog_ReturnsZero()
    {
        // Act
        var result = SyncLogRepository.GetMinVersion(_db);

        // Assert
        Assert.True(result is LongSyncOk);
        Assert.Equal(0, ((LongSyncOk)result).Value);
    }

    [Fact]
    public void SyncLogRepository_GetMinVersion_WithEntries_ReturnsMin()
    {
        // Arrange
        for (var i = 0; i < 5; i++)
        {
            DatabaseCommands.Execute(
                connection: _db,
                configure: command =>
                    command.CommandText =
                        $"INSERT INTO Person (Id, Name, Age) VALUES ('min{i}', 'Person {i}', {20 + i})"
            );
        }

        // Act
        var result = SyncLogRepository.GetMinVersion(_db);

        // Assert
        Assert.True(result is LongSyncOk);
        Assert.True(((LongSyncOk)result).Value >= 1);
    }

    [Fact]
    public void SyncLogRepository_GetEntryCount_EmptyLog_ReturnsZero()
    {
        // Act
        var result = SyncLogRepository.GetEntryCount(_db);

        // Assert
        Assert.True(result is LongSyncOk);
        Assert.Equal(0, ((LongSyncOk)result).Value);
    }

    [Fact]
    public void SyncLogRepository_GetEntryCount_WithEntries_ReturnsCount()
    {
        // Arrange
        for (var i = 0; i < 5; i++)
        {
            DatabaseCommands.Execute(
                connection: _db,
                configure: command =>
                    command.CommandText =
                        $"INSERT INTO Person (Id, Name, Age) VALUES ('cnt{i}', 'Person {i}', {20 + i})"
            );
        }

        // Act
        var result = SyncLogRepository.GetEntryCount(_db);

        // Assert
        Assert.True(result is LongSyncOk);
        Assert.Equal(5, ((LongSyncOk)result).Value);
    }

    [Fact]
    public void SyncLogRepository_FetchChanges_ContainsAllFields()
    {
        // Arrange
        DatabaseCommands.Execute(
            connection: _db,
            configure: command =>
                command.CommandText =
                    "INSERT INTO Person (Id, Name, Age) VALUES ('fld1', 'Alice', 30)"
        );

        // Act
        var result = SyncLogRepository.FetchChanges(_db, 0, 100);

        // Assert
        Assert.True(result is SyncLogListOk);
        var entries = ((SyncLogListOk)result).Value;
        Assert.NotEmpty(entries);

        var entry = entries[^1];
        Assert.True(entry.Version > 0);
        Assert.Equal("Person", entry.TableName);
        Assert.Contains("fld1", entry.PkValue);
        Assert.Equal(SyncOperation.Insert, entry.Operation);
        Assert.NotNull(entry.Payload);
        Assert.Equal(_originId, entry.Origin);
        Assert.NotEmpty(entry.Timestamp);
    }

    #endregion

    #region SubscriptionRepository Tests

    [Fact]
    public void SubscriptionRepository_GetAll_EmptyDatabase_ReturnsEmptyList()
    {
        // Act
        var result = SubscriptionRepository.GetAll(_db);

        // Assert
        Assert.True(result is SubscriptionListOk);
        Assert.Empty(((SubscriptionListOk)result).Value);
    }

    [Fact]
    public void SubscriptionRepository_Insert_ValidSubscription_Succeeds()
    {
        // Arrange
        var subscription = new SyncSubscription(
            "sub-1",
            _originId,
            SubscriptionType.Table,
            "Person",
            null,
            Timestamp,
            null
        );

        // Act
        var result = SubscriptionRepository.Insert(_db, subscription);

        // Assert
        Assert.True(result is BoolSyncOk);

        var all = SubscriptionRepository.GetAll(_db);
        Assert.Single(((SubscriptionListOk)all).Value);
    }

    [Fact]
    public void SubscriptionRepository_GetById_ExistingSubscription_ReturnsSubscription()
    {
        // Arrange
        var subscription = new SyncSubscription(
            "sub-1",
            _originId,
            SubscriptionType.Table,
            "Person",
            null,
            Timestamp,
            null
        );
        SubscriptionRepository.Insert(_db, subscription);

        // Act
        var result = SubscriptionRepository.GetById(_db, "sub-1");

        // Assert
        Assert.True(result is SubscriptionOk);
        var retrieved = ((SubscriptionOk)result).Value;
        Assert.NotNull(retrieved);
        Assert.Equal("sub-1", retrieved.SubscriptionId);
    }

    [Fact]
    public void SubscriptionRepository_GetById_NonExisting_ReturnsNull()
    {
        // Act
        var result = SubscriptionRepository.GetById(_db, "nonexistent");

        // Assert
        Assert.True(result is SubscriptionOk);
        Assert.Null(((SubscriptionOk)result).Value);
    }

    [Fact]
    public void SubscriptionRepository_GetByTable_FiltersCorrectly()
    {
        // Arrange
        SubscriptionRepository.Insert(
            _db,
            new SyncSubscription(
                "sub-1",
                _originId,
                SubscriptionType.Table,
                "Person",
                null,
                Timestamp,
                null
            )
        );
        SubscriptionRepository.Insert(
            _db,
            new SyncSubscription(
                "sub-2",
                _originId,
                SubscriptionType.Table,
                "Orders",
                null,
                Timestamp,
                null
            )
        );

        // Act
        var result = SubscriptionRepository.GetByTable(_db, "Person");

        // Assert
        Assert.True(result is SubscriptionListOk);
        var subs = ((SubscriptionListOk)result).Value;
        Assert.Single(subs);
        Assert.Equal("sub-1", subs[0].SubscriptionId);
    }

    [Fact]
    public void SubscriptionRepository_GetByOrigin_FiltersCorrectly()
    {
        // Arrange
        var origin1 = "origin-1";
        var origin2 = "origin-2";
        SubscriptionRepository.Insert(
            _db,
            new SyncSubscription(
                "sub-1",
                origin1,
                SubscriptionType.Table,
                "Person",
                null,
                Timestamp,
                null
            )
        );
        SubscriptionRepository.Insert(
            _db,
            new SyncSubscription(
                "sub-2",
                origin2,
                SubscriptionType.Table,
                "Person",
                null,
                Timestamp,
                null
            )
        );

        // Act
        var result = SubscriptionRepository.GetByOrigin(_db, origin1);

        // Assert
        Assert.True(result is SubscriptionListOk);
        var subs = ((SubscriptionListOk)result).Value;
        Assert.Single(subs);
        Assert.Equal("sub-1", subs[0].SubscriptionId);
    }

    [Fact]
    public void SubscriptionRepository_Delete_ExistingSubscription_Removes()
    {
        // Arrange
        SubscriptionRepository.Insert(
            _db,
            new SyncSubscription(
                "sub-1",
                _originId,
                SubscriptionType.Table,
                "Person",
                null,
                Timestamp,
                null
            )
        );

        // Act
        var result = SubscriptionRepository.Delete(_db, "sub-1");

        // Assert
        Assert.True(result is BoolSyncOk);

        var all = SubscriptionRepository.GetAll(_db);
        Assert.Empty(((SubscriptionListOk)all).Value);
    }

    [Fact]
    public void SubscriptionRepository_DeleteByOrigin_RemovesAll()
    {
        // Arrange
        var origin1 = "origin-1";
        SubscriptionRepository.Insert(
            _db,
            new SyncSubscription(
                "sub-1",
                origin1,
                SubscriptionType.Table,
                "Person",
                null,
                Timestamp,
                null
            )
        );
        SubscriptionRepository.Insert(
            _db,
            new SyncSubscription(
                "sub-2",
                origin1,
                SubscriptionType.Table,
                "Orders",
                null,
                Timestamp,
                null
            )
        );
        SubscriptionRepository.Insert(
            _db,
            new SyncSubscription(
                "sub-3",
                "origin-2",
                SubscriptionType.Table,
                "Products",
                null,
                Timestamp,
                null
            )
        );

        // Act
        var result = SubscriptionRepository.DeleteByOrigin(_db, origin1);

        // Assert
        Assert.True(result is IntSyncOk);
        Assert.Equal(2, ((IntSyncOk)result).Value);

        var remaining = SubscriptionRepository.GetAll(_db);
        Assert.Single(((SubscriptionListOk)remaining).Value);
    }

    [Fact]
    public void SubscriptionRepository_DeleteExpired_RemovesExpiredOnly()
    {
        // Arrange
        SubscriptionRepository.Insert(
            _db,
            new SyncSubscription(
                "sub-expired",
                _originId,
                SubscriptionType.Table,
                "Person",
                null,
                "2025-01-01T00:00:00Z",
                "2025-06-01T00:00:00Z"
            )
        );
        SubscriptionRepository.Insert(
            _db,
            new SyncSubscription(
                "sub-active",
                _originId,
                SubscriptionType.Table,
                "Orders",
                null,
                "2025-01-01T00:00:00Z",
                "2025-12-31T23:59:59Z"
            )
        );

        // Act
        var result = SubscriptionRepository.DeleteExpired(_db, "2025-07-01T00:00:00Z");

        // Assert
        Assert.True(result is IntSyncOk);
        Assert.Equal(1, ((IntSyncOk)result).Value);

        var remaining = SubscriptionRepository.GetAll(_db);
        Assert.Single(((SubscriptionListOk)remaining).Value);
        Assert.Equal("sub-active", ((SubscriptionListOk)remaining).Value[0].SubscriptionId);
    }

    #endregion

    #region Tombstone Integration Tests

    [Fact]
    public void TombstoneManager_CalculateSafeVersion_WithClients_ReturnsMinVersion()
    {
        // Arrange
        var clients = new List<SyncClient>
        {
            new("client-1", 100, Timestamp, Timestamp),
            new("client-2", 200, Timestamp, Timestamp),
            new("client-3", 300, Timestamp, Timestamp),
        };

        // Act
        var result = TombstoneManager.CalculateSafePurgeVersion(clients);

        // Assert
        Assert.Equal(100, result);
    }

    [Fact]
    public void TombstoneManager_PurgeTombstones_RemovesOldDeletes()
    {
        // Arrange - Insert and delete to create tombstones
        DatabaseCommands.Execute(
            connection: _db,
            configure: command =>
                command.CommandText = "INSERT INTO Person (Id, Name) VALUES ('p1', 'Alice')"
        );

        DatabaseCommands.Execute(
            connection: _db,
            configure: command => command.CommandText = "DELETE FROM Person WHERE Id = 'p1'"
        );

        // Get version
        var maxVersion = ((LongSyncOk)SyncLogRepository.GetMaxVersion(_db)).Value;

        // Use extension method to purge tombstones
        var result = _db.PurgeTombstones(maxVersion + 1);

        // Assert
        Assert.True(result is IntSyncOk);
        // Should have purged at least the delete tombstone
    }

    #endregion

    public void Dispose()
    {
        _db.Dispose();
        if (File.Exists(_dbPath))
        {
            try
            {
                File.Delete(_dbPath);
            }
            catch
            {
                /* File may be locked */
            }
        }
    }
}
