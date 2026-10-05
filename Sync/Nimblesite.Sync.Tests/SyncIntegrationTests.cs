using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Nimblesite.Sync.Tests;

/// <summary>
/// Real end-to-end integration tests syncing between two SQLite databases.
/// No mocks - actual data flowing between server and client DBs.
/// </summary>
public sealed class SyncIntegrationTests : IDisposable
{
    private static readonly ILogger Logger = NullLogger.Instance;
    private readonly string _serverDbPath = Path.Combine(
        Path.GetTempPath(),
        $"syncintegration_server_{Guid.NewGuid()}.db"
    );
    private readonly string _clientDbPath = Path.Combine(
        Path.GetTempPath(),
        $"syncintegration_client_{Guid.NewGuid()}.db"
    );
    private readonly SqliteConnection _serverDb;
    private readonly SqliteConnection _clientDb;
    private const string ServerOrigin = "server-origin-001";
    private const string ClientOrigin = "client-origin-001";

    public SyncIntegrationTests()
    {
        _serverDb = CreateSyncDatabase(ServerOrigin, _serverDbPath);
        _clientDb = CreateSyncDatabase(ClientOrigin, _clientDbPath);
    }

    [Fact]
    public void PullChanges_ServerHasNewRecords_ClientReceivesThem()
    {
        // Server has data
        InsertPerson(_serverDb, "p1", "Alice");
        InsertPerson(_serverDb, "p2", "Bob");

        // Sync: Pull from server to client
        var pullResult = PullChanges(_serverDb, _clientDb, ClientOrigin);

        Assert.True(pullResult > 0);
        Assert.Equal("Alice", GetPersonName(_clientDb, "p1"));
        Assert.Equal("Bob", GetPersonName(_clientDb, "p2"));
    }

    [Fact]
    public void PullChanges_ServerHasUpdates_ClientGetsUpdates()
    {
        // Initial sync
        InsertPerson(_serverDb, "p1", "Alice");
        PullChanges(_serverDb, _clientDb, ClientOrigin);

        // Server updates
        UpdatePerson(_serverDb, "p1", "Alice Updated");

        // Sync again
        var pullResult = PullChanges(_serverDb, _clientDb, ClientOrigin);

        Assert.True(pullResult > 0);
        Assert.Equal("Alice Updated", GetPersonName(_clientDb, "p1"));
    }

    [Fact]
    public void PullChanges_ServerDeletesRecord_ClientDeletesIt()
    {
        // Initial sync
        InsertPerson(_serverDb, "p1", "Alice");
        PullChanges(_serverDb, _clientDb, ClientOrigin);
        Assert.Equal("Alice", GetPersonName(_clientDb, "p1"));

        // Server deletes
        DeletePerson(_serverDb, "p1");

        // Sync again
        var pullResult = PullChanges(_serverDb, _clientDb, ClientOrigin);

        Assert.True(pullResult > 0);
        Assert.Null(GetPersonName(_clientDb, "p1"));
    }

    [Fact]
    public void PushChanges_ClientHasNewRecords_ServerReceivesThem()
    {
        // Client creates data locally
        InsertPerson(_clientDb, "p1", "Charlie");

        // Push from client to server
        var pushResult = PushChanges(_clientDb, _serverDb, ServerOrigin);

        Assert.True(pushResult > 0);
        Assert.Equal("Charlie", GetPersonName(_serverDb, "p1"));
    }

    [Fact]
    public void BiDirectionalSync_BothHaveChanges_BothGetUpdated()
    {
        // Server has Alice
        InsertPerson(_serverDb, "p1", "Alice");

        // Client has Bob (created offline)
        InsertPerson(_clientDb, "p2", "Bob");

        // Pull server -> client
        PullChanges(_serverDb, _clientDb, ClientOrigin);

        // Push client -> server
        PushChanges(_clientDb, _serverDb, ServerOrigin);

        // Both should have both records
        Assert.Equal("Alice", GetPersonName(_serverDb, "p1"));
        Assert.Equal("Bob", GetPersonName(_serverDb, "p2"));
        Assert.Equal("Alice", GetPersonName(_clientDb, "p1"));
        Assert.Equal("Bob", GetPersonName(_clientDb, "p2"));
    }

    [Fact]
    public void Sync_WithForeignKeys_HandlesCorrectOrder()
    {
        // Server has parent and child
        InsertParent(_serverDb, "parent1", "Parent One");
        InsertChild(_serverDb, "child1", "parent1", "Child One");

        // Pull to client - should handle FK order via defer/retry
        var pullResult = PullChanges(_serverDb, _clientDb, ClientOrigin);

        Assert.True(pullResult > 0);
        Assert.Equal("Parent One", GetParentName(_clientDb, "parent1"));
        Assert.Equal("Child One", GetChildName(_clientDb, "child1"));
    }

    [Fact]
    public void Sync_EchoPreventionWorks_NoInfiniteLoop()
    {
        // Server creates record
        InsertPerson(_serverDb, "p1", "Alice");

        // Get server's sync log count before
        var serverLogCountBefore = GetSyncLogCount(_serverDb);

        // Pull to client (should NOT create new log entries on server)
        PullChanges(_serverDb, _clientDb, ClientOrigin);

        // Push back (client got the record but it's from server origin, should skip)
        PushChanges(_clientDb, _serverDb, ServerOrigin);

        // Server log count should NOT have grown from the push
        var serverLogCountAfter = GetSyncLogCount(_serverDb);
        Assert.Equal(serverLogCountBefore, serverLogCountAfter);
    }

    [Fact]
    public void Sync_LargeBatch_ProcessesInBatches()
    {
        // Create 50 records on server
        for (var i = 1; i <= 50; i++)
        {
            InsertPerson(_serverDb, $"p{i}", $"Person {i}");
        }

        // Pull with small batch size
        var pullResult = PullChangesWithBatchSize(
            _serverDb,
            _clientDb,
            ClientOrigin,
            batchSize: 10
        );

        Assert.Equal(50, pullResult);
        Assert.Equal("Person 1", GetPersonName(_clientDb, "p1"));
        Assert.Equal("Person 50", GetPersonName(_clientDb, "p50"));
    }

    [Fact]
    public void HashVerification_AfterSync_HashesMatch()
    {
        // Create data on server
        InsertPerson(_serverDb, "p1", "Alice");
        InsertPerson(_serverDb, "p2", "Bob");

        // Sync to client
        PullChanges(_serverDb, _clientDb, ClientOrigin);

        // Compute hashes
        var serverHash = ComputePersonTableHash(_serverDb);
        var clientHash = ComputePersonTableHash(_clientDb);

        Assert.Equal(serverHash, clientHash);
    }

    // === Helper Methods ===

    private static SqliteConnection CreateSyncDatabase(string originId, string dbPath)
    {
        var conn = new SqliteConnection($"Data Source={dbPath}");
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"""
            PRAGMA foreign_keys = ON;

            CREATE TABLE _sync_state (
                key TEXT PRIMARY KEY,
                value TEXT NOT NULL
            );

            CREATE TABLE _sync_session (
                sync_active INTEGER DEFAULT 0
            );
            INSERT INTO _sync_session VALUES (0);

            CREATE TABLE _sync_log (
                version INTEGER PRIMARY KEY AUTOINCREMENT,
                table_name TEXT NOT NULL,
                pk_value TEXT NOT NULL,
                operation TEXT NOT NULL CHECK (operation IN ('insert', 'update', 'delete')),
                payload TEXT,
                origin TEXT NOT NULL,
                timestamp TEXT NOT NULL
            );

            CREATE INDEX idx_sync_log_version ON _sync_log(version);

            INSERT INTO _sync_state VALUES ('origin_id', '{originId}');
            INSERT INTO _sync_state VALUES ('last_server_version', '0');

            CREATE TABLE Person (
                Id TEXT PRIMARY KEY,
                Name TEXT NOT NULL
            );

            CREATE TABLE Parent (
                Id TEXT PRIMARY KEY,
                Name TEXT NOT NULL
            );

            CREATE TABLE Child (
                Id TEXT PRIMARY KEY,
                ParentId TEXT NOT NULL REFERENCES Parent(Id),
                Name TEXT NOT NULL
            );

            -- Sync triggers for Person
            CREATE TRIGGER Person_sync_insert AFTER INSERT ON Person
            WHEN (SELECT sync_active FROM _sync_session) = 0
            BEGIN
                INSERT INTO _sync_log (table_name, pk_value, operation, payload, origin, timestamp)
                VALUES ('Person', json_object('Id', NEW.Id), 'insert',
                        json_object('Id', NEW.Id, 'Name', NEW.Name),
                        (SELECT value FROM _sync_state WHERE key = 'origin_id'),
                        strftime('%Y-%m-%dT%H:%M:%fZ', 'now'));
            END;

            CREATE TRIGGER Person_sync_update AFTER UPDATE ON Person
            WHEN (SELECT sync_active FROM _sync_session) = 0
            BEGIN
                INSERT INTO _sync_log (table_name, pk_value, operation, payload, origin, timestamp)
                VALUES ('Person', json_object('Id', NEW.Id), 'update',
                        json_object('Id', NEW.Id, 'Name', NEW.Name),
                        (SELECT value FROM _sync_state WHERE key = 'origin_id'),
                        strftime('%Y-%m-%dT%H:%M:%fZ', 'now'));
            END;

            CREATE TRIGGER Person_sync_delete AFTER DELETE ON Person
            WHEN (SELECT sync_active FROM _sync_session) = 0
            BEGIN
                INSERT INTO _sync_log (table_name, pk_value, operation, payload, origin, timestamp)
                VALUES ('Person', json_object('Id', OLD.Id), 'delete', NULL,
                        (SELECT value FROM _sync_state WHERE key = 'origin_id'),
                        strftime('%Y-%m-%dT%H:%M:%fZ', 'now'));
            END;

            -- Sync triggers for Parent
            CREATE TRIGGER Parent_sync_insert AFTER INSERT ON Parent
            WHEN (SELECT sync_active FROM _sync_session) = 0
            BEGIN
                INSERT INTO _sync_log (table_name, pk_value, operation, payload, origin, timestamp)
                VALUES ('Parent', json_object('Id', NEW.Id), 'insert',
                        json_object('Id', NEW.Id, 'Name', NEW.Name),
                        (SELECT value FROM _sync_state WHERE key = 'origin_id'),
                        strftime('%Y-%m-%dT%H:%M:%fZ', 'now'));
            END;

            -- Sync triggers for Child
            CREATE TRIGGER Child_sync_insert AFTER INSERT ON Child
            WHEN (SELECT sync_active FROM _sync_session) = 0
            BEGIN
                INSERT INTO _sync_log (table_name, pk_value, operation, payload, origin, timestamp)
                VALUES ('Child', json_object('Id', NEW.Id), 'insert',
                        json_object('Id', NEW.Id, 'ParentId', NEW.ParentId, 'Name', NEW.Name),
                        (SELECT value FROM _sync_state WHERE key = 'origin_id'),
                        strftime('%Y-%m-%dT%H:%M:%fZ', 'now'));
            END;
            """;
        cmd.ExecuteNonQuery();

        return conn;
    }

    private static void InsertPerson(SqliteConnection db, string id, string name)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = "INSERT INTO Person (Id, Name) VALUES ($id, $name)";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.Parameters.AddWithValue("$name", name);
        cmd.ExecuteNonQuery();
    }

    private static void UpdatePerson(SqliteConnection db, string id, string name)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = "UPDATE Person SET Name = $name WHERE Id = $id";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.Parameters.AddWithValue("$name", name);
        cmd.ExecuteNonQuery();
    }

    private static void DeletePerson(SqliteConnection db, string id)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = "DELETE FROM Person WHERE Id = $id";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }

    private static string? GetPersonName(SqliteConnection db, string id)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = "SELECT Name FROM Person WHERE Id = $id";
        cmd.Parameters.AddWithValue("$id", id);
        return cmd.ExecuteScalar() as string;
    }

    private static void InsertParent(SqliteConnection db, string id, string name)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = "INSERT INTO Parent (Id, Name) VALUES ($id, $name)";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.Parameters.AddWithValue("$name", name);
        cmd.ExecuteNonQuery();
    }

    private static string? GetParentName(SqliteConnection db, string id)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = "SELECT Name FROM Parent WHERE Id = $id";
        cmd.Parameters.AddWithValue("$id", id);
        return cmd.ExecuteScalar() as string;
    }

    private static void InsertChild(SqliteConnection db, string id, string parentId, string name)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = "INSERT INTO Child (Id, ParentId, Name) VALUES ($id, $parentId, $name)";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.Parameters.AddWithValue("$parentId", parentId);
        cmd.Parameters.AddWithValue("$name", name);
        cmd.ExecuteNonQuery();
    }

    private static string? GetChildName(SqliteConnection db, string id)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = "SELECT Name FROM Child WHERE Id = $id";
        cmd.Parameters.AddWithValue("$id", id);
        return cmd.ExecuteScalar() as string;
    }

    private static int GetSyncLogCount(SqliteConnection db)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM _sync_log";
        return Convert.ToInt32(
            cmd.ExecuteScalar(),
            System.Globalization.CultureInfo.InvariantCulture
        );
    }

    private static long GetLastSyncVersion(SqliteConnection db)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = "SELECT value FROM _sync_state WHERE key = 'last_server_version'";
        return long.Parse(
            (string)cmd.ExecuteScalar()!,
            System.Globalization.CultureInfo.InvariantCulture
        );
    }

    private static void SetLastSyncVersion(SqliteConnection db, long version)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText =
            "UPDATE _sync_state SET value = $version WHERE key = 'last_server_version'";
        cmd.Parameters.AddWithValue(
            "$version",
            version.ToString(System.Globalization.CultureInfo.InvariantCulture)
        );
        cmd.ExecuteNonQuery();
    }

    private static void SetSyncActive(SqliteConnection db, bool active)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = "UPDATE _sync_session SET sync_active = $active";
        cmd.Parameters.AddWithValue("$active", active ? 1 : 0);
        cmd.ExecuteNonQuery();
    }

    private int PullChanges(
        SqliteConnection source,
        SqliteConnection target,
        string targetOrigin
    ) => PullChangesWithBatchSize(source, target, targetOrigin, 1000);

    private int PullChangesWithBatchSize(
        SqliteConnection source,
        SqliteConnection target,
        string targetOrigin,
        int batchSize
    )
    {
        var lastVersion = GetLastSyncVersion(target);
        var totalApplied = 0;

        // Enable trigger suppression on target
        SetSyncActive(target, true);

        try
        {
            var result = BatchManager.ProcessAllBatches(
                lastVersion,
                new BatchConfig(batchSize),
                (from, limit) =>
                    new SyncLogListOk(
                        TestDb.FetchChanges(db: source, fromVersion: from, limit: limit)
                    ),
                batch =>
                {
                    var applyResult = ChangeApplier.ApplyBatch(
                        batch,
                        targetOrigin,
                        3,
                        entry => ApplyChange(target, entry),
                        Logger
                    );
                    return applyResult;
                },
                version => SetLastSyncVersion(target, version),
                Logger
            );

            if (result is IntSyncOk success)
            {
                totalApplied = success.Value;
            }
        }
        finally
        {
            SetSyncActive(target, false);
        }

        return totalApplied;
    }

    private int PushChanges(
        SqliteConnection source,
        SqliteConnection target,
        string targetOrigin
    ) =>
        // For push, we read from source's log and apply to target
        // This is similar to pull but in reverse direction
        PullChanges(source, target, targetOrigin);

    private static BoolSyncResult ApplyChange(SqliteConnection db, SyncLogEntry entry)
    {
        try
        {
            using var cmd = db.CreateCommand();

            switch (entry.Operation)
            {
                case SyncOperation.Insert:
                    ApplyInsert(db, entry);
                    break;
                case SyncOperation.Update:
                    ApplyUpdate(db, entry);
                    break;
                case SyncOperation.Delete:
                    ApplyDelete(db, entry);
                    break;
            }

            return new BoolSyncOk(true);
        }
        catch (SqliteException ex) when (ChangeApplier.IsForeignKeyViolation(ex.Message))
        {
            return new BoolSyncOk(false); // Defer for retry
        }
        catch (Exception ex)
        {
            return new BoolSyncError(new SyncErrorDatabase(ex.Message));
        }
    }

    private static void ApplyInsert(SqliteConnection db, SyncLogEntry entry)
    {
        var payload = System.Text.Json.JsonSerializer.Deserialize<
            Dictionary<string, System.Text.Json.JsonElement>
        >(entry.Payload!);

        using var cmd = db.CreateCommand();

        switch (entry.TableName)
        {
            case "Person":
                cmd.CommandText = "INSERT OR REPLACE INTO Person (Id, Name) VALUES ($id, $name)";
                cmd.Parameters.AddWithValue("$id", payload!["Id"].GetString());
                cmd.Parameters.AddWithValue("$name", payload["Name"].GetString());
                break;
            case "Parent":
                cmd.CommandText = "INSERT OR REPLACE INTO Parent (Id, Name) VALUES ($id, $name)";
                cmd.Parameters.AddWithValue("$id", payload!["Id"].GetString());
                cmd.Parameters.AddWithValue("$name", payload["Name"].GetString());
                break;
            case "Child":
                cmd.CommandText =
                    "INSERT OR REPLACE INTO Child (Id, ParentId, Name) VALUES ($id, $parentId, $name)";
                cmd.Parameters.AddWithValue("$id", payload!["Id"].GetString());
                cmd.Parameters.AddWithValue("$parentId", payload["ParentId"].GetString());
                cmd.Parameters.AddWithValue("$name", payload["Name"].GetString());
                break;
        }

        cmd.ExecuteNonQuery();
    }

    private static void ApplyUpdate(SqliteConnection db, SyncLogEntry entry) =>
        // For simplicity, treat update same as insert (upsert)
        ApplyInsert(db, entry);

    private static void ApplyDelete(SqliteConnection db, SyncLogEntry entry)
    {
        var pk = System.Text.Json.JsonSerializer.Deserialize<
            Dictionary<string, System.Text.Json.JsonElement>
        >(entry.PkValue);

        using var cmd = db.CreateCommand();
        cmd.CommandText = $"DELETE FROM {entry.TableName} WHERE Id = $id";
        cmd.Parameters.AddWithValue("$id", pk!["Id"].GetString());
        cmd.ExecuteNonQuery();
    }

    private static string ComputePersonTableHash(SqliteConnection db)
    {
        var rows = new List<Dictionary<string, object?>>();

        using var cmd = db.CreateCommand();
        cmd.CommandText = "SELECT Id, Name FROM Person ORDER BY Id";

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            rows.Add(
                new Dictionary<string, object?>
                {
                    ["Id"] = reader.GetString(0),
                    ["Name"] = reader.GetString(1),
                }
            );
        }

        return HashVerifier.ComputeDatabaseHash(["Person"], _ => rows);
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
}
