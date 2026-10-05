using Xunit;

namespace Nimblesite.Sync.SQLite.Tests;

/// <summary>
/// Integration tests for SqliteConnectionSyncExtensions.
/// Tests all extension methods on SqliteConnection for sync operations.
/// NO MOCKS - real SQLite databases only!
/// </summary>
public sealed partial class SqliteExtensionIntegrationTests : IDisposable
{
    #region Subscription Extension Methods

    [Fact]
    public void GetAllSubscriptions_EmptyDatabase_ReturnsEmptyList()
    {
        // Act
        var result = _db.GetAllSubscriptions();

        // Assert
        Assert.True(result is SubscriptionListOk);
        var subscriptions = ((SubscriptionListOk)result).Value;
        Assert.Empty(subscriptions);
    }

    [Fact]
    public void InsertSubscription_ValidSubscription_Succeeds()
    {
        // Arrange
        var subscription = new SyncSubscription(
            SubscriptionId: "sub-1",
            OriginId: _originId,
            Type: SubscriptionType.Table,
            TableName: "Orders",
            Filter: null,
            CreatedAt: Timestamp,
            ExpiresAt: null
        );

        // Act
        var result = _db.InsertSubscription(subscription);

        // Assert
        Assert.True(result is BoolSyncOk);

        // Verify
        var allSubs = _db.GetAllSubscriptions();
        Assert.True(allSubs is SubscriptionListOk);
        Assert.Single(((SubscriptionListOk)allSubs).Value);
    }

    [Fact]
    public void GetAllSubscriptions_WithSubscriptions_ReturnsAll()
    {
        // Arrange
        var sub1 = new SyncSubscription(
            "sub-1",
            _originId,
            SubscriptionType.Table,
            "Orders",
            null,
            Timestamp,
            null
        );
        var sub2 = new SyncSubscription(
            "sub-2",
            _originId,
            SubscriptionType.Record,
            "Products",
            "[\"p1\"]",
            Timestamp,
            "2025-12-31T23:59:59Z"
        );
        _db.InsertSubscription(sub1);
        _db.InsertSubscription(sub2);

        // Act
        var result = _db.GetAllSubscriptions();

        // Assert
        Assert.True(result is SubscriptionListOk);
        var subscriptions = ((SubscriptionListOk)result).Value;
        Assert.Equal(2, subscriptions.Count);
    }

    [Fact]
    public void GetSubscriptionsByTable_FiltersCorrectly()
    {
        // Arrange
        var ordersSub = new SyncSubscription(
            "sub-orders",
            _originId,
            SubscriptionType.Table,
            "Orders",
            null,
            Timestamp,
            null
        );
        var productsSub = new SyncSubscription(
            "sub-products",
            _originId,
            SubscriptionType.Table,
            "Products",
            null,
            Timestamp,
            null
        );
        _db.InsertSubscription(ordersSub);
        _db.InsertSubscription(productsSub);

        // Act
        var result = _db.GetSubscriptionsByTable("Orders");

        // Assert
        Assert.True(result is SubscriptionListOk);
        var subscriptions = ((SubscriptionListOk)result).Value;
        Assert.Single(subscriptions);
        Assert.Equal("sub-orders", subscriptions[0].SubscriptionId);
    }

    [Fact]
    public void DeleteSubscription_ExistingSubscription_Removes()
    {
        // Arrange
        var subscription = new SyncSubscription(
            "sub-1",
            _originId,
            SubscriptionType.Table,
            "Orders",
            null,
            Timestamp,
            null
        );
        _db.InsertSubscription(subscription);

        // Act
        var result = _db.DeleteSubscription("sub-1");

        // Assert
        Assert.True(result is BoolSyncOk);

        var allSubs = _db.GetAllSubscriptions();
        Assert.Empty(((SubscriptionListOk)allSubs).Value);
    }

    [Fact]
    public void DeleteSubscriptionsByOrigin_RemovesAllForOrigin()
    {
        // Arrange
        var origin1 = "origin-1";
        var origin2 = "origin-2";
        _db.InsertSubscription(
            new SyncSubscription(
                "sub-1",
                origin1,
                SubscriptionType.Table,
                "Orders",
                null,
                Timestamp,
                null
            )
        );
        _db.InsertSubscription(
            new SyncSubscription(
                "sub-2",
                origin1,
                SubscriptionType.Table,
                "Products",
                null,
                Timestamp,
                null
            )
        );
        _db.InsertSubscription(
            new SyncSubscription(
                "sub-3",
                origin2,
                SubscriptionType.Table,
                "Customers",
                null,
                Timestamp,
                null
            )
        );

        // Act
        var result = _db.DeleteSubscriptionsByOrigin(origin1);

        // Assert
        Assert.True(result is IntSyncOk);
        Assert.Equal(2, ((IntSyncOk)result).Value);

        var remaining = _db.GetAllSubscriptions();
        Assert.Single(((SubscriptionListOk)remaining).Value);
        Assert.Equal("sub-3", ((SubscriptionListOk)remaining).Value[0].SubscriptionId);
    }

    [Fact]
    public void DeleteExpiredSubscriptions_RemovesExpired()
    {
        // Arrange
        var expiredSub = new SyncSubscription(
            "sub-expired",
            _originId,
            SubscriptionType.Table,
            "Orders",
            null,
            "2025-01-01T00:00:00Z",
            "2025-06-01T00:00:00Z"
        );
        var activeSub = new SyncSubscription(
            "sub-active",
            _originId,
            SubscriptionType.Table,
            "Products",
            null,
            "2025-01-01T00:00:00Z",
            "2025-12-31T23:59:59Z"
        );
        var noExpirySub = new SyncSubscription(
            "sub-no-expiry",
            _originId,
            SubscriptionType.Table,
            "Customers",
            null,
            "2025-01-01T00:00:00Z",
            null
        );
        _db.InsertSubscription(expiredSub);
        _db.InsertSubscription(activeSub);
        _db.InsertSubscription(noExpirySub);

        // Act
        var result = _db.DeleteExpiredSubscriptions("2025-07-01T00:00:00Z");

        // Assert
        Assert.True(result is IntSyncOk);
        Assert.Equal(1, ((IntSyncOk)result).Value);

        var remaining = _db.GetAllSubscriptions();
        Assert.Equal(2, ((SubscriptionListOk)remaining).Value.Count);
    }

    #endregion

    #region Tombstone Extension Methods

    [Fact]
    public void GetOldestSyncLogVersion_EmptyLog_ReturnsZero()
    {
        // Act
        var result = _db.GetOldestSyncLogVersion();

        // Assert
        Assert.True(result is LongSyncOk);
        Assert.Equal(0, ((LongSyncOk)result).Value);
    }

    [Fact]
    public void GetOldestSyncLogVersion_WithEntries_ReturnsMinVersion()
    {
        // Arrange - Insert sync log entries directly
        using var cmd = _db.CreateCommand();
        cmd.CommandText = """
            INSERT INTO _sync_log (table_name, pk_value, operation, payload, origin, timestamp)
            VALUES ('Orders', '{"Id":"o1"}', 'insert', '{}', @origin, @ts);
            INSERT INTO _sync_log (table_name, pk_value, operation, payload, origin, timestamp)
            VALUES ('Orders', '{"Id":"o2"}', 'insert', '{}', @origin, @ts);
            """;
        cmd.Parameters.AddWithValue("@origin", _originId);
        cmd.Parameters.AddWithValue("@ts", Timestamp);
        cmd.ExecuteNonQuery();

        // Act
        var result = _db.GetOldestSyncLogVersion();

        // Assert
        Assert.True(result is LongSyncOk);
        Assert.True(((LongSyncOk)result).Value >= 1);
    }

    [Fact]
    public void PurgeTombstones_RemovesDeletesBeforeVersion()
    {
        // Arrange - Insert some entries
        using var cmd = _db.CreateCommand();
        cmd.CommandText = """
            INSERT INTO _sync_log (table_name, pk_value, operation, payload, origin, timestamp)
            VALUES ('Orders', '{"Id":"o1"}', 'delete', NULL, @origin, @ts);
            INSERT INTO _sync_log (table_name, pk_value, operation, payload, origin, timestamp)
            VALUES ('Orders', '{"Id":"o2"}', 'insert', '{}', @origin, @ts);
            """;
        cmd.Parameters.AddWithValue("@origin", _originId);
        cmd.Parameters.AddWithValue("@ts", Timestamp);
        cmd.ExecuteNonQuery();

        // Get current max version
        var maxVersionResult = SyncLogRepository.GetMaxVersion(_db);
        var maxVersion = ((LongSyncOk)maxVersionResult).Value;

        // Act
        var result = _db.PurgeTombstones(maxVersion + 1);

        // Assert
        Assert.True(result is IntSyncOk);
        Assert.Equal(1, ((IntSyncOk)result).Value); // One delete removed

        // Verify insert still exists
        cmd.CommandText = "SELECT COUNT(*) FROM _sync_log WHERE operation = 'insert'";
        Assert.Equal(1L, cmd.ExecuteScalar());
    }

    [Fact]
    public void PurgeSyncLog_RemovesAllBeforeVersion()
    {
        // Arrange
        using var cmd = _db.CreateCommand();
        cmd.CommandText = """
            INSERT INTO _sync_log (table_name, pk_value, operation, payload, origin, timestamp)
            VALUES ('Orders', '{"Id":"o1"}', 'insert', '{}', @origin, @ts);
            INSERT INTO _sync_log (table_name, pk_value, operation, payload, origin, timestamp)
            VALUES ('Orders', '{"Id":"o2"}', 'update', '{}', @origin, @ts);
            """;
        cmd.Parameters.AddWithValue("@origin", _originId);
        cmd.Parameters.AddWithValue("@ts", Timestamp);
        cmd.ExecuteNonQuery();

        var maxVersionResult = SyncLogRepository.GetMaxVersion(_db);
        var maxVersion = ((LongSyncOk)maxVersionResult).Value;

        // Act
        var result = _db.PurgeSyncLog(maxVersion + 1);

        // Assert
        Assert.True(result is IntSyncOk);
        Assert.Equal(2, ((IntSyncOk)result).Value);

        cmd.CommandText = "SELECT COUNT(*) FROM _sync_log";
        Assert.Equal(0L, cmd.ExecuteScalar());
    }

    #endregion

    #region Subscription Type Parsing

    [Fact]
    public void InsertAndRetrieve_RecordSubscription_TypePreserved()
    {
        // Arrange
        var subscription = new SyncSubscription(
            "sub-1",
            _originId,
            SubscriptionType.Record,
            "Orders",
            "[\"o1\"]",
            Timestamp,
            null
        );
        var subs = InsertAndRetrieveSubscription(subscription: subscription);
        Assert.Equal(SubscriptionType.Record, subs[0].Type);
    }

    [Fact]
    public void InsertAndRetrieve_QuerySubscription_TypePreserved()
    {
        // Arrange
        var subscription = new SyncSubscription(
            "sub-1",
            _originId,
            SubscriptionType.Query,
            "Orders",
            "{\"status\":\"pending\"}",
            Timestamp,
            null
        );
        var subs = InsertAndRetrieveSubscription(subscription: subscription);
        Assert.Equal(SubscriptionType.Query, subs[0].Type);
    }

    [Fact]
    public void InsertAndRetrieve_WithExpiresAt_PreservesValue()
    {
        // Arrange
        var expiresAt = "2025-12-31T23:59:59.999Z";
        var subscription = new SyncSubscription(
            "sub-1",
            _originId,
            SubscriptionType.Table,
            "Orders",
            null,
            Timestamp,
            expiresAt
        );
        var subs = InsertAndRetrieveSubscription(subscription: subscription);
        Assert.Equal(expiresAt, subs[0].ExpiresAt);
    }

    #endregion

    // Implements [SYNC-TEST-SUBSCRIPTION-ROUNDTRIP].
    private IReadOnlyList<SyncSubscription> InsertAndRetrieveSubscription(
        SyncSubscription subscription
    )
    {
        _db.InsertSubscription(subscription);
        var result = _db.GetAllSubscriptions();
        Assert.True(result is SubscriptionListOk);
        var subs = Assert.IsType<SubscriptionListOk>(result).Value;
        Assert.Single(subs);
        return subs;
    }
}
