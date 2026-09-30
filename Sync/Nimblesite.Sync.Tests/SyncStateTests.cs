namespace Nimblesite.Sync.Tests;

/// <summary>
/// Tests for SyncState, SyncSession, and SyncClient records.
/// </summary>
public sealed class SyncStateTests
{
    [Fact]
    public void SyncState_CreatesCorrectRecord()
    {
        var state = new SyncState("origin-123", 100, 50);

        Assert.Equal("origin-123", state.OriginId);
        Assert.Equal(100, state.LastServerVersion);
        Assert.Equal(50, state.LastPushVersion);
    }

    [Fact]
    public void SyncState_RecordEquality()
    {
        var state1 = new SyncState("origin-123", 100, 50);
        var state2 = new SyncState("origin-123", 100, 50);
        var state3 = new SyncState("origin-456", 100, 50);

        Assert.Equal(state1, state2);
        Assert.NotEqual(state1, state3);
    }

    [Fact]
    public void SyncState_With_CreatesModifiedCopy()
    {
        var state = new SyncState("origin-123", 100, 50);
        var updated = state with { LastServerVersion = 200 };

        Assert.Equal("origin-123", updated.OriginId);
        Assert.Equal(200, updated.LastServerVersion);
        Assert.Equal(50, updated.LastPushVersion);
        Assert.NotEqual(state.LastServerVersion, updated.LastServerVersion);
    }

    [Fact]
    public void SyncSession_CreatesCorrectRecord()
    {
        var session = new SyncSession(true);
        Assert.True(session.SyncActive);

        var sessionInactive = new SyncSession(false);
        Assert.False(sessionInactive.SyncActive);
    }

    [Fact]
    public void SyncSession_RecordEquality()
    {
        var session1 = new SyncSession(true);
        var session2 = new SyncSession(true);
        var session3 = new SyncSession(false);

        Assert.Equal(session1, session2);
        Assert.NotEqual(session1, session3);
    }

    [Fact]
    public void SyncClient_CreatesCorrectRecord()
    {
        var client = new SyncClient(
            "client-001",
            100,
            "2024-01-01T00:00:00Z",
            "2024-01-01T00:00:00Z"
        );

        Assert.Equal("client-001", client.OriginId);
        Assert.Equal(100, client.LastSyncVersion);
        Assert.Equal("2024-01-01T00:00:00Z", client.LastSyncTimestamp);
        Assert.Equal("2024-01-01T00:00:00Z", client.CreatedAt);
    }

    [Fact]
    public void SyncClient_RecordEquality()
    {
        var client1 = new SyncClient("c1", 100, "2024-01-01T00:00:00Z", "2024-01-01T00:00:00Z");
        var client2 = new SyncClient("c1", 100, "2024-01-01T00:00:00Z", "2024-01-01T00:00:00Z");
        var client3 = new SyncClient("c2", 100, "2024-01-01T00:00:00Z", "2024-01-01T00:00:00Z");

        Assert.Equal(client1, client2);
        Assert.NotEqual(client1, client3);
    }

    [Fact]
    public void SyncClient_With_CreatesModifiedCopy()
    {
        var client = new SyncClient("c1", 100, "2024-01-01T00:00:00Z", "2024-01-01T00:00:00Z");
        var updated = client with { LastSyncVersion = 200 };

        Assert.Equal("c1", updated.OriginId);
        Assert.Equal(200, updated.LastSyncVersion);
        Assert.Equal("2024-01-01T00:00:00Z", updated.CreatedAt);
    }

    [Fact]
    public void SyncLogEntry_CreatesCorrectRecord()
    {
        var entry = new SyncLogEntry(
            1,
            "Person",
            "{\"Id\":\"p1\"}",
            SyncOperation.Insert,
            "{\"Id\":\"p1\",\"Name\":\"Alice\"}",
            "origin-1",
            "2024-01-01T00:00:00Z"
        );

        Assert.Equal(1, entry.Version);
        Assert.Equal("Person", entry.TableName);
        Assert.Equal("{\"Id\":\"p1\"}", entry.PkValue);
        Assert.Equal(SyncOperation.Insert, entry.Operation);
        Assert.Equal("{\"Id\":\"p1\",\"Name\":\"Alice\"}", entry.Payload);
        Assert.Equal("origin-1", entry.Origin);
        Assert.Equal("2024-01-01T00:00:00Z", entry.Timestamp);
    }

    [Fact]
    public void SyncLogEntry_DeleteHasNullPayload()
    {
        var entry = new SyncLogEntry(
            1,
            "Person",
            "{\"Id\":\"p1\"}",
            SyncOperation.Delete,
            null,
            "origin-1",
            "2024-01-01T00:00:00Z"
        );

        Assert.Null(entry.Payload);
        Assert.Equal(SyncOperation.Delete, entry.Operation);
    }

    [Fact]
    public void SyncOperation_HasCorrectValues()
    {
        Assert.Equal(SyncOperation.Insert, (SyncOperation)0);
        Assert.Equal(SyncOperation.Update, (SyncOperation)1);
        Assert.Equal(SyncOperation.Delete, (SyncOperation)2);
    }

    [Fact]
    public void SubscriptionType_HasCorrectValues()
    {
        Assert.Equal(SubscriptionType.Record, (SubscriptionType)0);
        Assert.Equal(SubscriptionType.Table, (SubscriptionType)1);
        Assert.Equal(SubscriptionType.Query, (SubscriptionType)2);
    }

    [Fact]
    public void SyncSubscription_CreatesCorrectRecord()
    {
        var sub = new SyncSubscription(
            "sub-1",
            "origin-1",
            SubscriptionType.Table,
            "Person",
            null,
            "2024-01-01T00:00:00Z",
            "2024-12-31T00:00:00Z"
        );

        Assert.Equal("sub-1", sub.SubscriptionId);
        Assert.Equal("origin-1", sub.OriginId);
        Assert.Equal(SubscriptionType.Table, sub.Type);
        Assert.Equal("Person", sub.TableName);
        Assert.Null(sub.Filter);
        Assert.Equal("2024-01-01T00:00:00Z", sub.CreatedAt);
        Assert.Equal("2024-12-31T00:00:00Z", sub.ExpiresAt);
    }

    [Fact]
    public void ChangeNotification_CreatesCorrectRecord()
    {
        var entry = new SyncLogEntry(
            1,
            "Person",
            "{\"Id\":\"p1\"}",
            SyncOperation.Insert,
            "{}",
            "origin",
            "2024-01-01T00:00:00Z"
        );

        var notification = new ChangeNotification("sub-1", entry);

        Assert.Equal("sub-1", notification.SubscriptionId);
        Assert.Equal(entry, notification.Change);
    }

    [Fact]
    public void PullResult_CreatesCorrectRecord()
    {
        var result = new PullResult(10, 0, 100);

        Assert.Equal(10, result.ChangesApplied);
        Assert.Equal(0, result.FromVersion);
        Assert.Equal(100, result.ToVersion);
    }

    [Fact]
    public void PushResult_CreatesCorrectRecord()
    {
        var result = new PushResult(5, 0, 50);

        Assert.Equal(5, result.ChangesPushed);
        Assert.Equal(0, result.FromVersion);
        Assert.Equal(50, result.ToVersion);
    }

    [Fact]
    public void SyncResult_CreatesCorrectRecord()
    {
        var pull = new PullResult(10, 0, 100);
        var push = new PushResult(5, 0, 50);
        var result = new SyncResult(pull, push);

        Assert.Equal(pull, result.Pull);
        Assert.Equal(push, result.Push);
    }

    [Fact]
    public void BatchConfig_CreatesCorrectRecord()
    {
        var config = new BatchConfig(100, 3);

        Assert.Equal(100, config.BatchSize);
        Assert.Equal(3, config.MaxRetryPasses);
    }

    [Fact]
    public void BatchConfig_DefaultMaxRetry()
    {
        var config = new BatchConfig(50);

        Assert.Equal(50, config.BatchSize);
        Assert.Equal(3, config.MaxRetryPasses); // Default
    }

    [Fact]
    public void BatchApplyResult_CreatesCorrectRecord()
    {
        var result = new BatchApplyResult(10, 2, 100L);

        Assert.Equal(10, result.AppliedCount);
        Assert.Equal(2, result.DeferredCount);
        Assert.Equal(100L, result.ToVersion);
    }

    [Fact]
    public void SyncBatch_CreatesCorrectRecord()
    {
        var changes = SingleInsertChange();
        var batch = new SyncBatch(changes, 0, 1, false);

        Assert.Single(batch.Changes);
        Assert.Equal(0, batch.FromVersion);
        Assert.Equal(1, batch.ToVersion);
        Assert.False(batch.HasMore);
    }

    [Fact]
    public void SyncBatch_HasMore_True()
    {
        var changes = SingleInsertChange();
        var batch = new SyncBatch(changes, 0, 1, true);

        Assert.True(batch.HasMore);
    }

    [Fact]
    public void SyncBatch_WithHash()
    {
        var changes = SingleInsertChange();
        var batch = new SyncBatch(changes, 0, 1, false, "abc123hash");

        Assert.Equal("abc123hash", batch.Hash);
    }

    [Fact]
    public void ConflictStrategy_HasCorrectValues()
    {
        Assert.Equal(ConflictStrategy.LastWriteWins, (ConflictStrategy)0);
        Assert.Equal(ConflictStrategy.ServerWins, (ConflictStrategy)1);
        Assert.Equal(ConflictStrategy.ClientWins, (ConflictStrategy)2);
    }

    private static IReadOnlyList<SyncLogEntry> SingleInsertChange() =>
        new List<SyncLogEntry> { new(1, "T", "PK", SyncOperation.Insert, "{}", "O", "TS") };
}
