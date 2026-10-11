namespace Nimblesite.Sync.Tests;

/// <summary>
/// Tests for SubscriptionManager.
/// </summary>
public sealed partial class SubscriptionManagerTests
{
    private const string Timestamp = "2024-01-01T00:00:00Z";

    #region Subscription Creation Tests

    [Fact]
    public void CreateRecordSubscription_CreatesCorrectSubscription()
    {
        var sub = SubscriptionManager.CreateRecordSubscription(
            "sub-1",
            "origin-1",
            "Person",
            "[\"p1\", \"p2\"]",
            Timestamp,
            "2024-12-31T00:00:00Z"
        );

        Assert.Equal("sub-1", sub.SubscriptionId);
        Assert.Equal("origin-1", sub.OriginId);
        Assert.Equal(SubscriptionType.Record, sub.Type);
        Assert.Equal("Person", sub.TableName);
        Assert.Equal("[\"p1\", \"p2\"]", sub.Filter);
        Assert.Equal(Timestamp, sub.CreatedAt);
        Assert.Equal("2024-12-31T00:00:00Z", sub.ExpiresAt);
    }

    [Fact]
    public void CreateRecordSubscription_WithoutExpiry()
    {
        var sub = SubscriptionManager.CreateRecordSubscription(
            "sub-1",
            "origin-1",
            "Person",
            "[\"p1\"]",
            Timestamp
        );

        Assert.Null(sub.ExpiresAt);
    }

    [Fact]
    public void CreateTableSubscription_CreatesCorrectSubscription()
    {
        var sub = SubscriptionManager.CreateTableSubscription(
            "sub-2",
            "origin-1",
            "Department",
            Timestamp,
            "2024-12-31T00:00:00Z"
        );

        Assert.Equal("sub-2", sub.SubscriptionId);
        Assert.Equal("origin-1", sub.OriginId);
        Assert.Equal(SubscriptionType.Table, sub.Type);
        Assert.Equal("Department", sub.TableName);
        Assert.Null(sub.Filter);
        Assert.Equal(Timestamp, sub.CreatedAt);
    }

    [Fact]
    public void CreateTableSubscription_WithoutExpiry()
    {
        var sub = SubscriptionManager.CreateTableSubscription(
            "sub-2",
            "origin-1",
            "Department",
            Timestamp
        );

        Assert.Null(sub.ExpiresAt);
    }

    [Fact]
    public void CreateQuerySubscription_CreatesCorrectSubscription()
    {
        var sub = SubscriptionManager.CreateQuerySubscription(
            "sub-3",
            "origin-1",
            "Orders",
            "{\"status\":\"active\"}",
            Timestamp,
            "2024-12-31T00:00:00Z"
        );

        Assert.Equal("sub-3", sub.SubscriptionId);
        Assert.Equal("origin-1", sub.OriginId);
        Assert.Equal(SubscriptionType.Query, sub.Type);
        Assert.Equal("Orders", sub.TableName);
        Assert.Equal("{\"status\":\"active\"}", sub.Filter);
        Assert.Equal(Timestamp, sub.CreatedAt);
    }

    [Fact]
    public void CreateQuerySubscription_WithoutExpiry()
    {
        var sub = SubscriptionManager.CreateQuerySubscription(
            "sub-3",
            "origin-1",
            "Orders",
            "{\"status\":\"active\"}",
            Timestamp
        );

        Assert.Null(sub.ExpiresAt);
    }

    #endregion

    #region MatchesChange Tests

    [Fact]
    public void MatchesChange_TableSubscription_MatchesSameTable()
    {
        var sub = SubscriptionManager.CreateTableSubscription(
            "sub-1",
            "origin-1",
            "Person",
            Timestamp
        );

        var change = CreateChange("Person", "{\"Id\":\"p1\"}");

        Assert.True(SubscriptionManager.MatchesChange(sub, change));
    }

    [Fact]
    public void MatchesChange_TableSubscription_DoesNotMatchDifferentTable()
    {
        var sub = SubscriptionManager.CreateTableSubscription(
            "sub-1",
            "origin-1",
            "Person",
            Timestamp
        );

        var change = CreateChange("Department", "{\"Id\":\"d1\"}");

        Assert.False(SubscriptionManager.MatchesChange(sub, change));
    }

    [Fact]
    public void MatchesChange_RecordSubscription_MatchesPkInFilter()
    {
        var sub = SubscriptionManager.CreateRecordSubscription(
            "sub-1",
            "origin-1",
            "Person",
            "[\"p1\", \"p2\"]",
            Timestamp
        );

        var change = CreateChange("Person", "{\"Id\":\"p1\"}");

        Assert.True(SubscriptionManager.MatchesChange(sub, change));
    }

    [Fact]
    public void MatchesChange_RecordSubscription_DoesNotMatchPkNotInFilter()
    {
        var sub = SubscriptionManager.CreateRecordSubscription(
            "sub-1",
            "origin-1",
            "Person",
            "[\"p1\", \"p2\"]",
            Timestamp
        );

        var change = CreateChange("Person", "{\"Id\":\"p99\"}");

        Assert.False(SubscriptionManager.MatchesChange(sub, change));
    }

    [Fact]
    public void MatchesChange_RecordSubscription_NullFilter_ReturnsFalse()
    {
        var sub = Dataset_MatchesChange_RecordSubscription_NullFilter_ReturnsFalse_1();

        var change = CreateChange("Person", "{\"Id\":\"p1\"}");

        Assert.False(SubscriptionManager.MatchesChange(sub, change));
    }

    [Fact]
    public void MatchesChange_RecordSubscription_EmptyFilter_ReturnsFalse()
    {
        var sub = Dataset_MatchesChange_RecordSubscription_EmptyFilter_ReturnsFalse_1();

        var change = CreateChange("Person", "{\"Id\":\"p1\"}");

        Assert.False(SubscriptionManager.MatchesChange(sub, change));
    }

    [Fact]
    public void MatchesChange_RecordSubscription_InvalidJsonPk_FallsBackToContains()
    {
        var sub = SubscriptionManager.CreateRecordSubscription(
            "sub-1",
            "origin-1",
            "Person",
            "simple-filter",
            Timestamp
        );

        var change = CreateChange("Person", "simple-filter");

        Assert.True(SubscriptionManager.MatchesChange(sub, change));
    }

    [Fact]
    public void MatchesChange_QuerySubscription_AlwaysMatchesSameTable()
    {
        var sub = SubscriptionManager.CreateQuerySubscription(
            "sub-1",
            "origin-1",
            "Orders",
            "{\"status\":\"active\"}",
            Timestamp
        );

        var change = CreateChange("Orders", "{\"Id\":\"o1\"}");

        // Query matching returns true for table match (app-level logic)
        Assert.True(SubscriptionManager.MatchesChange(sub, change));
    }

    [Fact]
    public void MatchesChange_QuerySubscription_DifferentTable_ReturnsFalse()
    {
        var sub = SubscriptionManager.CreateQuerySubscription(
            "sub-1",
            "origin-1",
            "Orders",
            "{\"status\":\"active\"}",
            Timestamp
        );

        var change = CreateChange("Products", "{\"Id\":\"p1\"}");

        Assert.False(SubscriptionManager.MatchesChange(sub, change));
    }

    #endregion

    #region IsExpired Tests

    [Fact]
    public void IsExpired_NoExpiry_ReturnsFalse()
    {
        var sub = SubscriptionManager.CreateTableSubscription(
            "sub-1",
            "origin-1",
            "Person",
            Timestamp
        );

        Assert.False(SubscriptionManager.IsExpired(sub, "2099-01-01T00:00:00Z"));
    }

    [Fact]
    public void IsExpired_FutureExpiry_ReturnsFalse()
    {
        var sub = SubscriptionManager.CreateTableSubscription(
            "sub-1",
            "origin-1",
            "Person",
            Timestamp,
            "2099-01-01T00:00:00Z"
        );

        Assert.False(SubscriptionManager.IsExpired(sub, "2024-06-01T00:00:00Z"));
    }

    [Fact]
    public void IsExpired_PastExpiry_ReturnsTrue()
    {
        var sub = SubscriptionManager.CreateTableSubscription(
            "sub-1",
            "origin-1",
            "Person",
            Timestamp,
            "2024-01-01T00:00:00Z"
        );

        Assert.True(SubscriptionManager.IsExpired(sub, "2024-06-01T00:00:00Z"));
    }

    [Fact]
    public void IsExpired_ExactExpiry_ReturnsFalse()
    {
        var sub = SubscriptionManager.CreateTableSubscription(
            "sub-1",
            "origin-1",
            "Person",
            Timestamp,
            "2024-06-01T00:00:00Z"
        );

        Assert.False(SubscriptionManager.IsExpired(sub, "2024-06-01T00:00:00Z"));
    }

    #endregion

    #region FindMatchingSubscriptions Tests

    [Fact]
    public void FindMatchingSubscriptions_ReturnsAllMatching()
    {
        var subs = new[]
        {
            SubscriptionManager.CreateTableSubscription("s1", "o1", "Person", Timestamp),
            SubscriptionManager.CreateTableSubscription("s2", "o1", "Person", Timestamp),
            SubscriptionManager.CreateTableSubscription("s3", "o1", "Department", Timestamp),
        };

        var change = CreateChange("Person", "{\"Id\":\"p1\"}");

        var matches = SubscriptionManager.FindMatchingSubscriptions(subs, change);

        Assert.Equal(2, matches.Count);
        Assert.Contains(matches, s => s.SubscriptionId == "s1");
        Assert.Contains(matches, s => s.SubscriptionId == "s2");
    }

    [Fact]
    public void FindMatchingSubscriptions_NoMatches_ReturnsEmpty()
    {
        var subs = new[]
        {
            SubscriptionManager.CreateTableSubscription("s1", "o1", "Person", Timestamp),
        };

        var change = CreateChange("Department", "{\"Id\":\"d1\"}");

        var matches = SubscriptionManager.FindMatchingSubscriptions(subs, change);

        Assert.Empty(matches);
    }

    [Fact]
    public void FindMatchingSubscriptions_EmptyInput_ReturnsEmpty()
    {
        var change = CreateChange("Person", "{\"Id\":\"p1\"}");

        var matches = SubscriptionManager.FindMatchingSubscriptions([], change);

        Assert.Empty(matches);
    }

    #endregion

    #region CreateNotifications Tests

    [Fact]
    public void CreateNotifications_CreatesForAllMatches()
    {
        var subs = new[]
        {
            SubscriptionManager.CreateTableSubscription("s1", "o1", "Person", Timestamp),
            SubscriptionManager.CreateTableSubscription("s2", "o1", "Person", Timestamp),
        };

        var change = CreateChange("Person", "{\"Id\":\"p1\"}");

        var notifications = SubscriptionManager.CreateNotifications(subs, change);

        Assert.Equal(2, notifications.Count);
        Assert.All(notifications, n => Assert.Equal(change, n.Change));
        Assert.Contains(notifications, n => n.SubscriptionId == "s1");
        Assert.Contains(notifications, n => n.SubscriptionId == "s2");
    }

    [Fact]
    public void CreateNotifications_NoMatches_ReturnsEmpty()
    {
        var subs = new[]
        {
            SubscriptionManager.CreateTableSubscription("s1", "o1", "Department", Timestamp),
        };

        var change = CreateChange("Person", "{\"Id\":\"p1\"}");

        var notifications = SubscriptionManager.CreateNotifications(subs, change);

        Assert.Empty(notifications);
    }

    #endregion

    #region FilterExpired Tests

    [Fact]
    public void FilterExpired_RemovesExpired()
    {
        var subs = new[]
        {
            SubscriptionManager.CreateTableSubscription(
                "s1",
                "o1",
                "Person",
                Timestamp,
                "2024-01-01T00:00:00Z"
            ),
            SubscriptionManager.CreateTableSubscription(
                "s2",
                "o1",
                "Person",
                Timestamp,
                "2099-01-01T00:00:00Z"
            ),
            SubscriptionManager.CreateTableSubscription("s3", "o1", "Person", Timestamp),
        };

        var active = SubscriptionManager.FilterExpired(subs, "2024-06-01T00:00:00Z");

        Assert.Equal(2, active.Count);
        Assert.Contains(active, s => s.SubscriptionId == "s2");
        Assert.Contains(active, s => s.SubscriptionId == "s3");
    }

    [Fact]
    public void FilterExpired_AllExpired_ReturnsEmpty()
    {
        var subs = new[]
        {
            SubscriptionManager.CreateTableSubscription(
                "s1",
                "o1",
                "Person",
                Timestamp,
                "2024-01-01T00:00:00Z"
            ),
            SubscriptionManager.CreateTableSubscription(
                "s2",
                "o1",
                "Person",
                Timestamp,
                "2024-01-02T00:00:00Z"
            ),
        };

        var active = SubscriptionManager.FilterExpired(subs, "2024-06-01T00:00:00Z");

        Assert.Empty(active);
    }

    [Fact]
    public void FilterExpired_NoneExpired_ReturnsAll()
    {
        var subs = new[]
        {
            SubscriptionManager.CreateTableSubscription(
                "s1",
                "o1",
                "Person",
                Timestamp,
                "2099-01-01T00:00:00Z"
            ),
            SubscriptionManager.CreateTableSubscription("s2", "o1", "Person", Timestamp),
        };

        var active = SubscriptionManager.FilterExpired(subs, "2024-06-01T00:00:00Z");

        Assert.Equal(2, active.Count);
    }

    #endregion

    #region Helper Methods

    private static SyncLogEntry CreateChange(string tableName, string pkValue) =>
        new(1, tableName, pkValue, SyncOperation.Insert, "{}", "test-origin", Timestamp);

    #endregion
}
