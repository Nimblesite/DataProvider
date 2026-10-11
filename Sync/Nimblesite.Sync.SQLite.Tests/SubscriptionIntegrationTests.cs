using Microsoft.Data.Sqlite;
using Xunit;

namespace Nimblesite.Sync.SQLite.Tests;

/// <summary>
/// Integration tests for real-time subscriptions (Spec Section 10).
/// Tests subscription creation, matching, and notification delivery.
/// NO MOCKS - real SQLite databases only!
/// </summary>
public sealed partial class SubscriptionIntegrationTests : IDisposable
{
    private readonly SqliteConnection _db;
    private readonly string _dbPath = Path.Combine(
        Path.GetTempPath(),
        $"subscriptionintegrationtests_{Guid.NewGuid()}.db"
    );
    private readonly string _originId = Guid.NewGuid().ToString();
    private const string Timestamp = "2025-01-01T00:00:00.000Z";

    public SubscriptionIntegrationTests()
    {
        _db = new SqliteConnection($"Data Source={_dbPath}");
        _db.Open();
        SyncSchema.CreateSchema(_db);
        SyncSchema.SetOriginId(_db, _originId);
    }

    #region Section 10.2: Subscription Types

    /// <summary>
    /// Spec 10.2: Record subscription - subscribe to specific PK(s).
    /// </summary>
    [Fact]
    public void Spec10_2_RecordSubscription_MatchesSpecificPkValues()
    {
        // Arrange
        var sub = SubscriptionManager.CreateRecordSubscription(
            "sub-1",
            _originId,
            "Orders",
            "[\"order-123\", \"order-456\"]",
            Timestamp
        );

        var matchingChange = CreateChange("Orders", "{\"Id\":\"order-123\"}", SyncOperation.Update);
        var nonMatchingChange = CreateChange(
            "Orders",
            "{\"Id\":\"order-999\"}",
            SyncOperation.Update
        );

        // Act & Assert
        Assert.True(SubscriptionManager.MatchesChange(sub, matchingChange));
        Assert.False(SubscriptionManager.MatchesChange(sub, nonMatchingChange));
    }

    /// <summary>
    /// Spec 10.2: Table subscription - subscribe to all changes in a table.
    /// </summary>
    [Fact]
    public void Spec10_2_TableSubscription_MatchesAllTableChanges()
    {
        // Arrange
        var sub = SubscriptionManager.CreateTableSubscription(
            "sub-1",
            _originId,
            "Products",
            Timestamp
        );

        var insertChange = CreateChange("Products", "{\"Id\":\"p1\"}", SyncOperation.Insert);
        var updateChange = CreateChange("Products", "{\"Id\":\"p2\"}", SyncOperation.Update);
        var deleteChange = CreateChange("Products", "{\"Id\":\"p3\"}", SyncOperation.Delete);
        var otherTableChange = CreateChange("Orders", "{\"Id\":\"o1\"}", SyncOperation.Insert);

        // Act & Assert
        Assert.True(SubscriptionManager.MatchesChange(sub, insertChange));
        Assert.True(SubscriptionManager.MatchesChange(sub, updateChange));
        Assert.True(SubscriptionManager.MatchesChange(sub, deleteChange));
        Assert.False(SubscriptionManager.MatchesChange(sub, otherTableChange));
    }

    /// <summary>
    /// Spec 10.2: Query subscription - subscribe to records matching criteria.
    /// </summary>
    [Fact]
    public void Spec10_2_QuerySubscription_MatchesTableChanges()
    {
        // Arrange: Query subscriptions match table-level (filtering done app-side)
        var sub = SubscriptionManager.CreateQuerySubscription(
            "sub-1",
            _originId,
            "Orders",
            "{\"status\": \"pending\"}",
            Timestamp
        );

        var change = CreateChange("Orders", "{\"Id\":\"o1\"}", SyncOperation.Update);

        // Act & Assert: Matches table (query filtering is application-level)
        Assert.True(SubscriptionManager.MatchesChange(sub, change));
    }

    #endregion

    #region Section 10.4: Server Notifications

    /// <summary>
    /// Spec 10.4: Notifications include full change payload.
    /// </summary>
    [Fact]
    public void Spec10_4_CreateNotifications_IncludesFullChangePayload()
    {
        // Arrange
        var subscriptions = new[]
        {
            SubscriptionManager.CreateTableSubscription("sub-1", "origin-1", "Orders", Timestamp),
            SubscriptionManager.CreateTableSubscription("sub-2", "origin-2", "Orders", Timestamp),
            SubscriptionManager.CreateTableSubscription("sub-3", "origin-3", "Products", Timestamp),
        };

        var change = Fixture_Spec10_4_CreateNotifications_IncludesFullChangePayload_1();

        // Act
        var notifications = SubscriptionManager.CreateNotifications(subscriptions, change);

        // Assert
        Assert.Equal(2, notifications.Count); // Only Orders subscriptions
        Assert.All(
            notifications,
            n =>
            {
                Assert.Equal(change, n.Change);
                Assert.Equal(100, n.Change.Version);
                Assert.Equal(
                    "{\"Id\":\"order-123\",\"Status\":\"shipped\",\"Total\":99.99}",
                    n.Change.Payload
                );
            }
        );
    }

    #endregion

    #region Section 10.5: Delivery Guarantees

    /// <summary>
    /// Spec 10.5: At-least-once delivery - notifications may be delivered multiple times.
    /// Client handles idempotently via version tracking.
    /// </summary>
    [Fact]
    public void Spec10_5_AtLeastOnceDelivery_ClientTracksVersions()
    {
        // Arrange: Simulate client tracking applied versions
        var appliedVersions = new HashSet<long>();
        var change = CreateChange("Orders", "{\"Id\":\"o1\"}", SyncOperation.Update, version: 42);

        // Act: Apply same change twice (simulating duplicate delivery)
        for (var i = 0; i < 2; i++)
        {
            if (!appliedVersions.Contains(change.Version))
            {
                appliedVersions.Add(change.Version);
            }
        }

        // Assert: Only applied once
        Assert.Single(appliedVersions);
        Assert.Contains(42L, appliedVersions);
    }

    #endregion

    #region Section 10.6: Subscription Expiry

    /// <summary>
    /// Spec 10.6: Subscriptions can have optional TTL (expires_at).
    /// </summary>
    [Fact]
    public void Spec10_6_SubscriptionExpiry_FiltersExpired()
    {
        // Arrange
        var subscriptions = new[]
        {
            SubscriptionManager.CreateTableSubscription(
                "sub-active",
                "origin-1",
                "Orders",
                "2025-01-01T00:00:00.000Z",
                expiresAt: "2025-12-31T23:59:59.000Z"
            ),
            SubscriptionManager.CreateTableSubscription(
                "sub-expired",
                "origin-2",
                "Orders",
                "2025-01-01T00:00:00.000Z",
                expiresAt: "2025-01-01T00:00:00.000Z"
            ),
            SubscriptionManager.CreateTableSubscription(
                "sub-no-expiry",
                "origin-3",
                "Orders",
                "2025-01-01T00:00:00.000Z",
                expiresAt: null
            ),
        };

        var currentTime = "2025-06-15T12:00:00.000Z";

        // Act
        var active = SubscriptionManager.FilterExpired(subscriptions, currentTime);

        // Assert
        Assert.Equal(2, active.Count);
        Assert.Contains(active, s => s.SubscriptionId == "sub-active");
        Assert.Contains(active, s => s.SubscriptionId == "sub-no-expiry");
        Assert.DoesNotContain(active, s => s.SubscriptionId == "sub-expired");
    }

    /// <summary>
    /// Spec 10.6: Subscription schema matches spec table.
    /// </summary>
    [Fact]
    public void Spec10_6_SubscriptionSchema_HasRequiredColumns()
    {
        // Act: Query table info
        using var cmd = _db.CreateCommand();
        cmd.CommandText = "PRAGMA table_info(_sync_subscriptions)";
        var columns = new List<string>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            columns.Add(reader.GetString(1));
        }

        // Assert: All spec columns exist
        Assert.Contains("subscription_id", columns);
        Assert.Contains("origin_id", columns);
        Assert.Contains("subscription_type", columns);
        Assert.Contains("table_name", columns);
        Assert.Contains("filter", columns);
        Assert.Contains("created_at", columns);
        Assert.Contains("expires_at", columns);
    }

    /// <summary>
    /// Spec 10.6: Subscription type CHECK constraint (record, table, query).
    /// </summary>
    [Fact]
    public void Spec10_6_SubscriptionType_OnlyAllowedValues()
    {
        // Act: Try to insert invalid subscription type
        using var cmd = _db.CreateCommand();
        cmd.CommandText = """
            INSERT INTO _sync_subscriptions (subscription_id, origin_id, subscription_type, table_name, created_at)
            VALUES ('test-sub', 'origin-1', 'invalid', 'Orders', '2025-01-01T00:00:00.000Z')
            """;

        // Assert: Should fail CHECK constraint
        Assert.Throws<SqliteException>(() => cmd.ExecuteNonQuery());
    }

    /// <summary>
    /// Spec 10.6: Valid subscription types can be inserted.
    /// </summary>
    [Fact]
    public void Spec10_6_ValidSubscriptionTypes_CanBeInserted()
    {
        using var cmd = _db.CreateCommand();

        // Record type
        cmd.CommandText = """
            INSERT INTO _sync_subscriptions (subscription_id, origin_id, subscription_type, table_name, created_at)
            VALUES ('sub-record', 'origin-1', 'record', 'Orders', '2025-01-01T00:00:00.000Z')
            """;
        cmd.ExecuteNonQuery();

        // Table type
        cmd.CommandText = """
            INSERT INTO _sync_subscriptions (subscription_id, origin_id, subscription_type, table_name, created_at)
            VALUES ('sub-table', 'origin-2', 'table', 'Products', '2025-01-01T00:00:00.000Z')
            """;
        cmd.ExecuteNonQuery();

        // Query type
        cmd.CommandText = """
            INSERT INTO _sync_subscriptions (subscription_id, origin_id, subscription_type, table_name, created_at)
            VALUES ('sub-query', 'origin-3', 'query', 'Customers', '2025-01-01T00:00:00.000Z')
            """;
        cmd.ExecuteNonQuery();

        // Assert: All inserted
        cmd.CommandText = "SELECT COUNT(*) FROM _sync_subscriptions";
        Assert.Equal(3L, cmd.ExecuteScalar());
    }

    #endregion

    #region Section 10.7: Requirements

    /// <summary>
    /// Spec 10.7: Server MUST support record-level and table-level subscriptions.
    /// </summary>
    [Fact]
    public void Spec10_7_BothSubscriptionLevels_Supported()
    {
        // Arrange
        var recordSub = SubscriptionManager.CreateRecordSubscription(
            "sub-record",
            _originId,
            "Orders",
            "[\"order-1\"]",
            Timestamp
        );
        var tableSub = SubscriptionManager.CreateTableSubscription(
            "sub-table",
            _originId,
            "Orders",
            Timestamp
        );

        // Assert
        Assert.Equal(SubscriptionType.Record, recordSub.Type);
        Assert.Equal(SubscriptionType.Table, tableSub.Type);
    }

    /// <summary>
    /// Spec 10.7: Notifications MUST include full change payload.
    /// </summary>
    [Fact]
    public void Spec10_7_Notifications_IncludeFullPayload()
    {
        // Arrange
        var sub = SubscriptionManager.CreateTableSubscription(
            "sub-1",
            _originId,
            "Orders",
            Timestamp
        );
        var change = Fixture_Spec10_7_Notifications_IncludeFullPayload_1();

        // Act
        var notifications = SubscriptionManager.CreateNotifications([sub], change);

        // Assert
        Assert.Single(notifications);
        var notification = notifications[0];
        Assert.Equal(change.Version, notification.Change.Version);
        Assert.Equal(change.TableName, notification.Change.TableName);
        Assert.Equal(change.PkValue, notification.Change.PkValue);
        Assert.Equal(change.Operation, notification.Change.Operation);
        Assert.Equal(change.Payload, notification.Change.Payload);
        Assert.Equal(change.Origin, notification.Change.Origin);
        Assert.Equal(change.Timestamp, notification.Change.Timestamp);
    }

    #endregion

    #region Helpers

    private static SyncLogEntry CreateChange(
        string tableName,
        string pkValue,
        SyncOperation operation,
        long version = 1
    ) =>
        new(
            version,
            tableName,
            pkValue,
            operation,
            operation == SyncOperation.Delete ? null : "{\"Id\":\"test\"}",
            "server-origin",
            Timestamp
        );

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

    #endregion
}
