// Implements [CI-TEST-SHARED-COMMAND-PARAMETERS].
using Tests.Shared;
using Xunit;
using static global::Tests.Shared.ResultAssertions;

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

        var subscriptions = Ok(actual: result).Value;
        Assert.Empty(subscriptions);
    }

    [Fact]
    public void InsertSubscription_ValidSubscription_Succeeds()
    {
        // Arrange
        var subscription = Dataset_InsertSubscription_ValidSubscription_Succeeds_1();

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
        var sub1 = Dataset_GetAllSubscriptions_WithSubscriptions_ReturnsAll_1();
        var sub2 = Dataset_GetAllSubscriptions_WithSubscriptions_ReturnsAll_2();
        _db.InsertSubscription(sub1);
        _db.InsertSubscription(sub2);

        // Act
        var result = _db.GetAllSubscriptions();

        // Assert

        var subscriptions = Ok(actual: result).Value;
        Assert.Equal(2, subscriptions.Count);
    }

    [Fact]
    public void GetSubscriptionsByTable_FiltersCorrectly()
    {
        // Arrange
        var ordersSub = Dataset_GetSubscriptionsByTable_FiltersCorrectly_1();
        var productsSub = Dataset_GetSubscriptionsByTable_FiltersCorrectly_2();
        _db.InsertSubscription(ordersSub);
        _db.InsertSubscription(productsSub);

        // Act
        var result = _db.GetSubscriptionsByTable("Orders");

        // Assert

        var subscriptions = Ok(actual: result).Value;
        Assert.Single(subscriptions);
        Assert.Equal("sub-orders", subscriptions[0].SubscriptionId);
    }

    [Fact]
    public void DeleteSubscription_ExistingSubscription_Removes()
    {
        // Arrange
        var subscription = Dataset_DeleteSubscription_ExistingSubscription_Removes_1();
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
            Dataset_DeleteSubscriptionsByOrigin_RemovesAllForOrigin_1(origin1: origin1)
        );
        _db.InsertSubscription(
            Dataset_DeleteSubscriptionsByOrigin_RemovesAllForOrigin_2(origin1: origin1)
        );
        _db.InsertSubscription(
            Dataset_DeleteSubscriptionsByOrigin_RemovesAllForOrigin_3(origin2: origin2)
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
        var expiredSub = Dataset_DeleteExpiredSubscriptions_RemovesExpired_1();
        var activeSub = Dataset_DeleteExpiredSubscriptions_RemovesExpired_2();
        var noExpirySub = Dataset_DeleteExpiredSubscriptions_RemovesExpired_3();
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
        DatabaseCommands.Execute(
            connection: _db,
            configure: cmd =>
                cmd.CommandText = Data_GetOldestSyncLogVersion_WithEntries_ReturnsMinVersion_1,
            parameters: () => [("@origin", _originId), ("@ts", Timestamp)]
        );

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
        cmd.CommandText = Data_PurgeTombstones_RemovesDeletesBeforeVersion_1;
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
        cmd.CommandText = Data_PurgeSyncLog_RemovesAllBeforeVersion_1;
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
        var subscription = Dataset_InsertAndRetrieve_RecordSubscription_TypePreserved_1();
        var subs = InsertAndRetrieveSubscription(subscription: subscription);
        Assert.Equal(SubscriptionType.Record, subs[0].Type);
    }

    [Fact]
    public void InsertAndRetrieve_QuerySubscription_TypePreserved()
    {
        // Arrange
        var subscription = Dataset_InsertAndRetrieve_QuerySubscription_TypePreserved_1();
        var subs = InsertAndRetrieveSubscription(subscription: subscription);
        Assert.Equal(SubscriptionType.Query, subs[0].Type);
    }

    [Fact]
    public void InsertAndRetrieve_WithExpiresAt_PreservesValue()
    {
        // Arrange
        var expiresAt = "2025-12-31T23:59:59.999Z";
        var subscription = Dataset_InsertAndRetrieve_WithExpiresAt_PreservesValue_1(
            expiresAt: expiresAt
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
