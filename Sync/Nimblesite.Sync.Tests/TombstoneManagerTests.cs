namespace Nimblesite.Sync.Tests;

public sealed partial class TombstoneManagerTests
{
    [Fact]
    public void CalculateSafePurgeVersion_EmptyClients_ReturnsZero()
    {
        var result = TombstoneManager.CalculateSafePurgeVersion([]);

        Assert.Equal(0, result);
    }

    [Fact]
    public void CalculateSafePurgeVersion_SingleClient_ReturnsClientVersion()
    {
        var clients = new[]
        {
            new SyncClient("origin-1", 100, "2025-01-01T00:00:00.000Z", "2025-01-01T00:00:00.000Z"),
        };

        var result = TombstoneManager.CalculateSafePurgeVersion(clients);

        Assert.Equal(100, result);
    }

    [Fact]
    public void CalculateSafePurgeVersion_MultipleClients_ReturnsMinimum()
    {
        var clients = new[]
        {
            new SyncClient("origin-1", 500, "2025-01-01T00:00:00.000Z", "2025-01-01T00:00:00.000Z"),
            new SyncClient("origin-2", 100, "2025-01-01T00:00:00.000Z", "2025-01-01T00:00:00.000Z"),
            new SyncClient("origin-3", 300, "2025-01-01T00:00:00.000Z", "2025-01-01T00:00:00.000Z"),
        };

        var result = TombstoneManager.CalculateSafePurgeVersion(clients);

        Assert.Equal(100, result);
    }

    [Fact]
    public void RequiresFullResync_ClientAhead_ReturnsFalse()
    {
        var result = TombstoneManager.RequiresFullResync(
            clientLastVersion: 100,
            oldestAvailableVersion: 50
        );

        Assert.False(result);
    }

    [Fact]
    public void RequiresFullResync_ClientBehind_ReturnsTrue()
    {
        var result = TombstoneManager.RequiresFullResync(
            clientLastVersion: 50,
            oldestAvailableVersion: 100
        );

        Assert.True(result);
    }

    [Fact]
    public void RequiresFullResync_ClientAtOldest_ReturnsFalse()
    {
        var result = TombstoneManager.RequiresFullResync(
            clientLastVersion: 100,
            oldestAvailableVersion: 100
        );

        Assert.False(result);
    }

    [Fact]
    public void FindStaleClients_NoStaleClients_ReturnsEmpty()
    {
        var now = new DateTime(2025, 1, 15, 0, 0, 0, DateTimeKind.Utc);
        var clients = new[]
        {
            new SyncClient("origin-1", 100, "2025-01-10T00:00:00.000Z", "2025-01-01T00:00:00.000Z"),
        };

        var result = TombstoneManager.FindStaleClients(clients, now, TimeSpan.FromDays(90));

        Assert.Empty(result);
    }

    [Fact]
    public void FindStaleClients_StaleClient_ReturnsOriginId()
    {
        var now = new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var clients = new[]
        {
            new SyncClient("origin-1", 100, "2025-01-01T00:00:00.000Z", "2025-01-01T00:00:00.000Z"),
            new SyncClient("origin-2", 200, "2025-05-01T00:00:00.000Z", "2025-01-01T00:00:00.000Z"),
        };

        var result = TombstoneManager.FindStaleClients(clients, now, TimeSpan.FromDays(90));

        Assert.Single(result);
        Assert.Equal("origin-1", result[0]);
    }

    [Fact]
    public void FindStaleClients_AllStale_ReturnsAll()
    {
        var now = new DateTime(2025, 12, 1, 0, 0, 0, DateTimeKind.Utc);
        var clients = new[]
        {
            new SyncClient("origin-1", 100, "2025-01-01T00:00:00.000Z", "2025-01-01T00:00:00.000Z"),
            new SyncClient("origin-2", 200, "2025-02-01T00:00:00.000Z", "2025-01-01T00:00:00.000Z"),
        };

        var result = TombstoneManager.FindStaleClients(clients, now, TimeSpan.FromDays(90));

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void PurgeTombstones_NoClients_ReturnsZeroPurged()
    {
        var purgedVersion = -1L;

        var result = TombstoneManager.PurgeTombstones(
            [],
            version =>
            {
                purgedVersion = version;
                return new IntSyncOk(0);
            }
        );

        Assert.IsType<IntSyncOk>(result);
        Assert.Equal(-1, purgedVersion); // Never called
    }

    [Fact]
    public void PurgeTombstones_WithClients_PurgesUpToMinVersion()
    {
        var clients = new[]
        {
            new SyncClient("origin-1", 500, "2025-01-01T00:00:00.000Z", "2025-01-01T00:00:00.000Z"),
            new SyncClient("origin-2", 100, "2025-01-01T00:00:00.000Z", "2025-01-01T00:00:00.000Z"),
        };
        var purgedVersion = -1L;

        var result = TombstoneManager.PurgeTombstones(
            clients,
            version =>
            {
                purgedVersion = version;
                return new IntSyncOk(50);
            }
        );

        Assert.IsType<IntSyncOk>(result);
        Assert.Equal(100, purgedVersion);
        Assert.Equal(50, ((IntSyncOk)result).Value);
    }

    [Fact]
    public void UpdateClientSyncState_NewClient_CreatesRecord()
    {
        var result = TombstoneManager.UpdateClientSyncState(
            "new-origin",
            100,
            "2025-01-01T00:00:00.000Z",
            existingClient: null
        );

        Assert.Equal("new-origin", result.OriginId);
        Assert.Equal(100, result.LastSyncVersion);
        Assert.Equal("2025-01-01T00:00:00.000Z", result.LastSyncTimestamp);
        Assert.Equal("2025-01-01T00:00:00.000Z", result.CreatedAt);
    }

    [Fact]
    public void UpdateClientSyncState_ExistingClient_UpdatesVersionAndTimestamp()
    {
        var existing = Dataset_UpdateClientSyncState_ExistingClient_UpdatesVersionAndTimestamp_1();

        var result = TombstoneManager.UpdateClientSyncState(
            "origin-1",
            100,
            "2025-01-01T00:00:00.000Z",
            existing
        );

        Assert.Equal("origin-1", result.OriginId);
        Assert.Equal(100, result.LastSyncVersion);
        Assert.Equal("2025-01-01T00:00:00.000Z", result.LastSyncTimestamp);
        Assert.Equal("2024-01-01T00:00:00.000Z", result.CreatedAt); // Preserved
    }

    [Fact]
    public void CreateFullResyncError_ReturnsCorrectError()
    {
        var error = TombstoneManager.CreateFullResyncError(50, 100);

        Assert.Equal(50, error.ClientVersion);
        Assert.Equal(100, error.OldestAvailableVersion);
    }
}
