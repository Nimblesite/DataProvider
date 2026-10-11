// Implements [CI-TEST-SHARED-COMMAND].
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Microsoft.Data.Sqlite;
using Tests.Shared;
using Xunit;

namespace Nimblesite.Sync.SQLite.Tests;

/// <summary>
/// Integration tests for tombstone retention (Spec Section 13).
/// Tests safe purge calculation, stale client handling, and full resync protocol.
/// NO MOCKS - real SQLite databases only!
/// </summary>
public sealed partial class TombstoneIntegrationTests : IDisposable
{
    private readonly SqliteConnection _serverDb;
    private readonly SqliteConnection _clientDb;
    private readonly string _serverDbPath = Path.Combine(
        Path.GetTempPath(),
        $"tombstoneintegrationtests_server_{Guid.NewGuid()}.db"
    );
    private readonly string _clientDbPath = Path.Combine(
        Path.GetTempPath(),
        $"tombstoneintegrationtests_client_{Guid.NewGuid()}.db"
    );
    private readonly string _serverOrigin = "server-" + Guid.NewGuid();
    private readonly string _clientOrigin = "client-" + Guid.NewGuid();

    public TombstoneIntegrationTests()
    {
        _serverDb = CreateSyncDatabase(_serverDbPath, _serverOrigin);
        _clientDb = CreateSyncDatabase(_clientDbPath, _clientOrigin);
    }

    #region Section 13.3: Server Tracking

    /// <summary>
    /// Spec 13.3: Server tracks last sync version for each known origin.
    /// </summary>
    [Fact]
    public void Spec13_3_SyncClients_SchemaMatches()
    {
        // Act: Query table info
        using var cmd = _serverDb.CreateCommand();
        cmd.CommandText = "PRAGMA table_info(_sync_clients)";
        var columns = new List<string>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            columns.Add(reader.GetString(1));
        }

        // Assert: All spec columns exist
        Assert.Contains("origin_id", columns);
        Assert.Contains("last_sync_version", columns);
        Assert.Contains("last_sync_timestamp", columns);
        Assert.Contains("created_at", columns);
    }

    /// <summary>
    /// Spec 13.3: Server updates client sync state after each sync.
    /// </summary>
    [Fact]
    public void Spec13_3_UpdateClientSyncState_TracksVersion()
    {
        // Arrange
        var existingClient = new SyncClient(
            _clientOrigin,
            50,
            "2024-12-01T00:00:00.000Z",
            "2024-01-01T00:00:00.000Z"
        );

        // Act
        var updated = TombstoneManager.UpdateClientSyncState(
            _clientOrigin,
            100,
            "2025-01-01T00:00:00.000Z",
            existingClient
        );

        // Assert
        Assert.Equal(_clientOrigin, updated.OriginId);
        Assert.Equal(100, updated.LastSyncVersion);
        Assert.Equal("2025-01-01T00:00:00.000Z", updated.LastSyncTimestamp);
        Assert.Equal("2024-01-01T00:00:00.000Z", updated.CreatedAt); // Preserved
    }

    #endregion

    #region Section 13.4: Safe Purge Calculation

    /// <summary>
    /// Spec 13.4: Find minimum version that ALL known clients have synced past.
    /// </summary>
    [Fact]
    public void Spec13_4_SafePurgeVersion_IsMinimumOfAllClients()
    {
        // Arrange: Multiple clients at different versions
        var clients = new[]
        {
            new SyncClient("client-1", 500, "2025-01-01T00:00:00.000Z", "2025-01-01T00:00:00.000Z"),
            new SyncClient("client-2", 100, "2025-01-01T00:00:00.000Z", "2025-01-01T00:00:00.000Z"), // Slowest
            new SyncClient("client-3", 300, "2025-01-01T00:00:00.000Z", "2025-01-01T00:00:00.000Z"),
        };

        // Act
        var safePurgeVersion = TombstoneManager.CalculateSafePurgeVersion(clients);

        // Assert: Safe to purge up to slowest client's version
        Assert.Equal(100, safePurgeVersion);
    }

    /// <summary>
    /// Spec 13.4: Tombstones can be purged when older than safe purge version.
    /// </summary>
    [Fact]
    public void Spec13_4_PurgeTombstones_UsesMinimumVersion()
    {
        // Arrange
        var clients = new[]
        {
            new SyncClient("client-1", 500, "2025-01-01T00:00:00.000Z", "2025-01-01T00:00:00.000Z"),
            new SyncClient("client-2", 200, "2025-01-01T00:00:00.000Z", "2025-01-01T00:00:00.000Z"),
        };
        var purgedVersion = -1L;

        // Act
        var result = TombstoneManager.PurgeTombstones(
            clients,
            version =>
            {
                purgedVersion = version;
                return new IntSyncOk(10); // Simulated purge count
            }
        );

        // Assert
        Assert.IsType<IntSyncOk>(result);
        Assert.Equal(200, purgedVersion); // Called with minimum version
    }

    #endregion

    #region Section 13.5: Stale Client Handling

    /// <summary>
    /// Spec 13.5: Clients inactive > 90 days are considered stale.
    /// </summary>
    [Fact]
    public void Spec13_5_StaleClients_IdentifiedByInactivity()
    {
        // Arrange: Client last synced 100 days ago
        var now = new DateTime(2025, 4, 10, 0, 0, 0, DateTimeKind.Utc);
        var clients = new[]
        {
            new SyncClient(
                "stale-client",
                100,
                "2025-01-01T00:00:00.000Z", // 100 days ago
                "2024-01-01T00:00:00.000Z"
            ),
            new SyncClient(
                "active-client",
                200,
                "2025-04-01T00:00:00.000Z", // 9 days ago
                "2024-01-01T00:00:00.000Z"
            ),
        };

        // Act
        var staleClients = TombstoneManager.FindStaleClients(clients, now, TimeSpan.FromDays(90));

        // Assert
        Assert.Single(staleClients);
        Assert.Equal("stale-client", staleClients[0]);
    }

    #endregion

    #region Section 13.6: Full Resync Protocol

    /// <summary>
    /// Spec 13.6: Client behind oldest available version requires full resync.
    /// </summary>
    [Fact]
    public void Spec13_6_RequiresFullResync_WhenClientBehind()
    {
        // Arrange: Client at version 50, oldest available is 100
        var clientLastVersion = 50L;
        var oldestAvailableVersion = 100L;

        // Act
        var needsResync = TombstoneManager.RequiresFullResync(
            clientLastVersion,
            oldestAvailableVersion
        );

        // Assert
        Assert.True(needsResync);
    }

    /// <summary>
    /// Spec 13.6: Client ahead of or at oldest available version is OK.
    /// </summary>
    [Fact]
    public void Spec13_6_NoResyncNeeded_WhenClientAheadOrEqual()
    {
        // Arrange: Client at or ahead of oldest
        Assert.False(TombstoneManager.RequiresFullResync(100, 50)); // Ahead
        Assert.False(TombstoneManager.RequiresFullResync(100, 100)); // Equal
    }

    /// <summary>
    /// Spec 13.6: Full resync error provides both versions for client feedback.
    /// </summary>
    [Fact]
    public void Spec13_6_FullResyncError_ContainsVersionInfo()
    {
        // Act
        var error = TombstoneManager.CreateFullResyncError(50, 100);

        // Assert
        Assert.Equal(50, error.ClientVersion);
        Assert.Equal(100, error.OldestAvailableVersion);
    }

    /// <summary>
    /// Spec 13.6: Demonstrates full resync workflow end-to-end.
    /// </summary>
    [Fact]
    public void Spec13_6_FullResync_EndToEndWorkflow()
    {
        // Arrange: Server has data, some tombstones purged
        CreatePersonTable(_serverDb);
        TriggerGenerator.CreateTriggers(_serverDb, "Person", TestLogger.L);

        // Insert then delete to create tombstone
        ExecuteSql(_serverDb, "INSERT INTO Person (Id, Name) VALUES ('p1', 'Alice')");
        ExecuteSql(_serverDb, "DELETE FROM Person WHERE Id = 'p1'");

        // Insert fresh data
        ExecuteSql(_serverDb, "INSERT INTO Person (Id, Name) VALUES ('p2', 'Bob')");

        // Simulate tombstone purge (delete entries < version 2)
        ExecuteSql(_serverDb, "DELETE FROM _sync_log WHERE version < 2");

        // Client tries to sync from version 0 (they missed the purged entries)
        var oldestAvailable = GetOldestVersion(_serverDb);
        var clientLastVersion = 0L;

        // Act: Check if resync needed
        var needsResync = TombstoneManager.RequiresFullResync(clientLastVersion, oldestAvailable);

        // Assert
        Assert.True(needsResync);

        // In real workflow: client would wipe local data and download current state
    }

    #endregion

    #region Section 13.7: Requirements

    /// <summary>
    /// Spec 13.7: Server MUST track last sync version per origin.
    /// </summary>
    [Fact]
    public void Spec13_7_ClientSyncState_CanBeStoredAndRetrieved()
    {
        // Arrange
        var timestamp = "2025-01-01T00:00:00.000Z";

        // Act: Store client state
        ExecuteSql(
            _serverDb,
            $"""
            INSERT INTO _sync_clients (origin_id, last_sync_version, last_sync_timestamp, created_at)
            VALUES ('{_clientOrigin}', 100, '{timestamp}', '{timestamp}')
            """
        );

        // Retrieve
        using var cmd = _serverDb.CreateCommand();
        cmd.CommandText = "SELECT last_sync_version FROM _sync_clients WHERE origin_id = $origin";
        cmd.Parameters.AddWithValue("$origin", _clientOrigin);
        var storedVersion = Convert.ToInt64(cmd.ExecuteScalar(), CultureInfo.InvariantCulture);

        // Assert
        Assert.Equal(100, storedVersion);
    }

    /// <summary>
    /// Spec 13.7: Tombstones MUST NOT be purged until all active clients have synced past them.
    /// </summary>
    [Fact]
    public void Spec13_7_TombstonesPreserved_UntilAllClientsSynced()
    {
        // Arrange: Two clients, one behind
        var clients = new[]
        {
            new SyncClient(
                "fast-client",
                100,
                "2025-01-01T00:00:00.000Z",
                "2025-01-01T00:00:00.000Z"
            ),
            new SyncClient(
                "slow-client",
                20,
                "2025-01-01T00:00:00.000Z",
                "2025-01-01T00:00:00.000Z"
            ), // Can't purge past this
        };

        // Act
        var safePurgeVersion = TombstoneManager.CalculateSafePurgeVersion(clients);

        // Assert: Can only purge tombstones with version <= 20
        Assert.Equal(20, safePurgeVersion);
    }

    #endregion

    #region Helpers

    private static SqliteConnection CreateSyncDatabase(string dbPath, string originId)
    {
        var conn = new SqliteConnection($"Data Source={dbPath}");
        conn.Open();
        SyncSchema.CreateSchema(conn);
        SyncSchema.SetOriginId(conn, originId);
        return conn;
    }

    private static void CreatePersonTable(SqliteConnection db)
    {
        DatabaseCommands.Execute(
            connection: db,
            configure: command =>
                command.CommandText = Data_CreatePersonTable_1
        );
    }

    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "Test helper for internal test SQL"
    )]
    private static void ExecuteSql(SqliteConnection db, string sql)
    {
        DatabaseCommands.Execute(connection: db, configure: command => command.CommandText = sql);
    }

    private static long GetOldestVersion(SqliteConnection db)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = "SELECT MIN(version) FROM _sync_log";
        var result = cmd.ExecuteScalar();
        return result == DBNull.Value ? 0 : Convert.ToInt64(result, CultureInfo.InvariantCulture);
    }

    public void Dispose()
    {
        _serverDb.Dispose();
        _clientDb.Dispose();

        if (File.Exists(_serverDbPath))
        {
            try
            {
                File.Delete(_serverDbPath);
            }
            catch
            {
                /* File may be locked */
            }
        }

        if (File.Exists(_clientDbPath))
        {
            try
            {
                File.Delete(_clientDbPath);
            }
            catch
            {
                /* File may be locked */
            }
        }
    }

    #endregion
}
