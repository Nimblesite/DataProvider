using System.Globalization;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Nimblesite.Sync.Tests;

/// <summary>
/// Integration tests for SyncCoordinator.
/// Uses real SQLite databases - no mocks.
/// </summary>
public sealed class SyncCoordinatorTests : IDisposable
{
    private static readonly ILogger Logger = NullLogger.Instance;
    private readonly string _serverDbPath = Path.Combine(
        Path.GetTempPath(),
        $"synccoord_server_{Guid.NewGuid()}.db"
    );
    private readonly string _clientDbPath = Path.Combine(
        Path.GetTempPath(),
        $"synccoord_client_{Guid.NewGuid()}.db"
    );
    private readonly SqliteConnection _serverDb;
    private readonly SqliteConnection _clientDb;
    private const string ServerOrigin = "server-coord-001";
    private const string ClientOrigin = "client-coord-001";

    public SyncCoordinatorTests()
    {
        _serverDb = CreateSyncDatabase(ServerOrigin, _serverDbPath);
        _clientDb = CreateSyncDatabase(ClientOrigin, _clientDbPath);
    }

    #region Pull Tests

    [Fact]
    public void Pull_EmptyServer_ReturnsZeroChanges()
    {
        var lastVersion = 0L;

        var result = SyncCoordinator.Pull(
            ClientOrigin,
            lastVersion,
            new BatchConfig(100),
            (from, limit) => FetchChanges(_serverDb, from, limit),
            entry => ApplyChange(_clientDb, entry),
            () => EnableSuppression(_clientDb),
            () => DisableSuppression(_clientDb),
            v => SetLastSyncVersion(_clientDb, v),
            Logger
        );

        Assert.IsType<PullResultOk>(result);
        var pull = ((PullResultOk)result).Value;
        Assert.Equal(0, pull.ChangesApplied);
        Assert.Equal(0, pull.FromVersion);
        Assert.Equal(0, pull.ToVersion);
    }

    [Fact]
    public void Pull_ServerHasChanges_AppliesAll()
    {
        // Server has data
        InsertPerson(_serverDb, "p1", "Alice");
        InsertPerson(_serverDb, "p2", "Bob");

        var result = SyncCoordinator.Pull(
            ClientOrigin,
            0,
            new BatchConfig(100),
            (from, limit) => FetchChanges(_serverDb, from, limit),
            entry => ApplyChange(_clientDb, entry),
            () => EnableSuppression(_clientDb),
            () => DisableSuppression(_clientDb),
            v => SetLastSyncVersion(_clientDb, v),
            Logger
        );

        Assert.IsType<PullResultOk>(result);
        var pull = ((PullResultOk)result).Value;
        Assert.Equal(2, pull.ChangesApplied);
        Assert.Equal(0, pull.FromVersion);
        Assert.True(pull.ToVersion > 0);

        Assert.Equal("Alice", GetPersonName(_clientDb, "p1"));
        Assert.Equal("Bob", GetPersonName(_clientDb, "p2"));
    }

    [Fact]
    public void Pull_SkipsOwnOriginChanges_EchoPrevention()
    {
        // Insert on server
        InsertPerson(_serverDb, "p1", "Alice");

        // Manually insert a change with CLIENT origin into server's sync log
        InsertSyncLogEntry(
            _serverDb,
            "Person",
            "{\"Id\":\"p2\"}",
            "insert",
            "{\"Id\":\"p2\",\"Name\":\"FromClient\"}",
            ClientOrigin
        );

        var result = SyncCoordinator.Pull(
            ClientOrigin,
            0,
            new BatchConfig(100),
            (from, limit) => FetchChanges(_serverDb, from, limit),
            entry => ApplyChange(_clientDb, entry),
            () => EnableSuppression(_clientDb),
            () => DisableSuppression(_clientDb),
            v => SetLastSyncVersion(_clientDb, v),
            Logger
        );

        Assert.IsType<PullResultOk>(result);
        var pull = ((PullResultOk)result).Value;
        // Only 1 change applied (Alice), p2 skipped because it's from own origin
        Assert.Equal(1, pull.ChangesApplied);
        Assert.Equal("Alice", GetPersonName(_clientDb, "p1"));
        Assert.Null(GetPersonName(_clientDb, "p2")); // Should NOT be applied
    }

    [Fact]
    public void Pull_MultipleBatches_ProcessesAll()
    {
        // Create 25 records
        for (var i = 1; i <= 25; i++)
        {
            InsertPerson(_serverDb, $"p{i}", $"Person {i}");
        }

        var result = SyncCoordinator.Pull(
            ClientOrigin,
            0,
            new BatchConfig(10), // Small batches
            (from, limit) => FetchChanges(_serverDb, from, limit),
            entry => ApplyChange(_clientDb, entry),
            () => EnableSuppression(_clientDb),
            () => DisableSuppression(_clientDb),
            v => SetLastSyncVersion(_clientDb, v),
            Logger
        );

        Assert.IsType<PullResultOk>(result);
        var pull = ((PullResultOk)result).Value;
        Assert.Equal(25, pull.ChangesApplied);

        // Verify all records
        for (var i = 1; i <= 25; i++)
        {
            Assert.Equal($"Person {i}", GetPersonName(_clientDb, $"p{i}"));
        }
    }

    [Fact]
    public void Pull_WithForeignKeys_RetriesDeferred()
    {
        // Create Parent first, then Child (FK dependency)
        InsertParent(_serverDb, "parent1", "Parent One");
        InsertChild(_serverDb, "child1", "parent1", "Child One");

        var result = SyncCoordinator.Pull(
            ClientOrigin,
            0,
            new BatchConfig(100, 3), // Allow retries
            (from, limit) => FetchChanges(_serverDb, from, limit),
            entry => ApplyChange(_clientDb, entry),
            () => EnableSuppression(_clientDb),
            () => DisableSuppression(_clientDb),
            v => SetLastSyncVersion(_clientDb, v),
            Logger
        );

        Assert.IsType<PullResultOk>(result);
        var pull = ((PullResultOk)result).Value;
        Assert.Equal(2, pull.ChangesApplied);

        Assert.Equal("Parent One", GetParentName(_clientDb, "parent1"));
        Assert.Equal("Child One", GetChildName(_clientDb, "child1"));
    }

    [Fact]
    public void Pull_TriggerSuppressionFailure_ReturnsError()
    {
        var result = SyncCoordinator.Pull(
            ClientOrigin,
            0,
            new BatchConfig(100),
            (from, limit) => FetchChanges(_serverDb, from, limit),
            entry => ApplyChange(_clientDb, entry),
            () => new BoolSyncError(new SyncErrorDatabase("Suppression failed")),
            () => DisableSuppression(_clientDb),
            v => SetLastSyncVersion(_clientDb, v),
            Logger
        );

        Assert.IsType<PullResultError>(result);
        var error = ((PullResultError)result).Value;
        Assert.IsType<SyncErrorDatabase>(error);
    }

    [Fact]
    public void Pull_FetchError_ReturnsError()
    {
        var result = SyncCoordinator.Pull(
            ClientOrigin,
            0,
            new BatchConfig(100),
            (from, limit) => new SyncLogListError(new SyncErrorDatabase("Fetch failed")),
            entry => ApplyChange(_clientDb, entry),
            () => EnableSuppression(_clientDb),
            () => DisableSuppression(_clientDb),
            v => SetLastSyncVersion(_clientDb, v),
            Logger
        );

        Assert.IsType<PullResultError>(result);
    }

    [Fact]
    public void Pull_UpdatesVersion_Correctly()
    {
        InsertPerson(_serverDb, "p1", "Alice");

        var capturedVersion = 0L;
        _ = SyncCoordinator.Pull(
            ClientOrigin,
            0,
            new BatchConfig(100),
            (from, limit) => FetchChanges(_serverDb, from, limit),
            entry => ApplyChange(_clientDb, entry),
            () => EnableSuppression(_clientDb),
            () => DisableSuppression(_clientDb),
            v => capturedVersion = v,
            Logger
        );

        Assert.True(capturedVersion > 0);
    }

    #endregion

    #region Push Tests

    [Fact]
    public void Push_EmptyClient_ReturnsZeroChanges()
    {
        var result = SyncCoordinator.Push(
            0,
            new BatchConfig(100),
            (from, limit) => FetchChanges(_clientDb, from, limit),
            changes => ApplyChangesToTarget(_serverDb, changes, ServerOrigin),
            v => SetLastPushVersion(_clientDb, v),
            Logger
        );

        Assert.IsType<PushResultOk>(result);
        var push = ((PushResultOk)result).Value;
        Assert.Equal(0, push.ChangesPushed);
    }

    [Fact]
    public void Push_ClientHasChanges_PushesAll()
    {
        InsertPerson(_clientDb, "p1", "Charlie");
        InsertPerson(_clientDb, "p2", "Diana");

        var result = SyncCoordinator.Push(
            0,
            new BatchConfig(100),
            (from, limit) => FetchChanges(_clientDb, from, limit),
            changes => ApplyChangesToTarget(_serverDb, changes, ServerOrigin),
            v => SetLastPushVersion(_clientDb, v),
            Logger
        );

        Assert.IsType<PushResultOk>(result);
        var push = ((PushResultOk)result).Value;
        Assert.Equal(2, push.ChangesPushed);

        Assert.Equal("Charlie", GetPersonName(_serverDb, "p1"));
        Assert.Equal("Diana", GetPersonName(_serverDb, "p2"));
    }

    [Fact]
    public void Push_MultipleBatches_PushesAll()
    {
        for (var i = 1; i <= 30; i++)
        {
            InsertPerson(_clientDb, $"p{i}", $"Person {i}");
        }

        var result = SyncCoordinator.Push(
            0,
            new BatchConfig(10),
            (from, limit) => FetchChanges(_clientDb, from, limit),
            changes => ApplyChangesToTarget(_serverDb, changes, ServerOrigin),
            v => SetLastPushVersion(_clientDb, v),
            Logger
        );

        Assert.IsType<PushResultOk>(result);
        var push = ((PushResultOk)result).Value;
        Assert.Equal(30, push.ChangesPushed);
    }

    [Fact]
    public void Push_SendFailure_ReturnsError()
    {
        InsertPerson(_clientDb, "p1", "Alice");

        var result = SyncCoordinator.Push(
            0,
            new BatchConfig(100),
            (from, limit) => FetchChanges(_clientDb, from, limit),
            changes => new BoolSyncError(new SyncErrorDatabase("Send failed")),
            v => SetLastPushVersion(_clientDb, v),
            Logger
        );

        Assert.IsType<PushResultError>(result);
    }

    #endregion

    #region Bidirectional Sync Tests

    [Fact]
    public void Sync_PullThenPush_Both()
    {
        // Server has Alice
        InsertPerson(_serverDb, "s1", "ServerAlice");

        // Client has Bob
        InsertPerson(_clientDb, "c1", "ClientBob");

        var result = SyncCoordinator.Sync(
            ClientOrigin,
            0,
            0,
            new BatchConfig(100),
            (from, limit) => FetchChanges(_serverDb, from, limit),
            entry => ApplyChange(_clientDb, entry),
            () => EnableSuppression(_clientDb),
            () => DisableSuppression(_clientDb),
            v => SetLastSyncVersion(_clientDb, v),
            (from, limit) => FetchChanges(_clientDb, from, limit),
            changes => ApplyChangesToTarget(_serverDb, changes, ServerOrigin),
            v => SetLastPushVersion(_clientDb, v),
            Logger
        );

        Assert.IsType<SyncResultOk>(result);
        var sync = ((SyncResultOk)result).Value;
        Assert.Equal(1, sync.Pull.ChangesApplied);
        Assert.Equal(1, sync.Push.ChangesPushed);

        // Client has both
        Assert.Equal("ServerAlice", GetPersonName(_clientDb, "s1"));
        Assert.Equal("ClientBob", GetPersonName(_clientDb, "c1"));

        // Server has both
        Assert.Equal("ServerAlice", GetPersonName(_serverDb, "s1"));
        Assert.Equal("ClientBob", GetPersonName(_serverDb, "c1"));
    }

    [Fact]
    public void Sync_PullFailure_ReturnsError()
    {
        var result = SyncCoordinator.Sync(
            ClientOrigin,
            0,
            0,
            new BatchConfig(100),
            (from, limit) => new SyncLogListError(new SyncErrorDatabase("Pull failed")),
            entry => ApplyChange(_clientDb, entry),
            () => EnableSuppression(_clientDb),
            () => DisableSuppression(_clientDb),
            v => SetLastSyncVersion(_clientDb, v),
            (from, limit) => FetchChanges(_clientDb, from, limit),
            changes => ApplyChangesToTarget(_serverDb, changes, ServerOrigin),
            v => SetLastPushVersion(_clientDb, v),
            Logger
        );

        Assert.IsType<SyncResultError>(result);
    }

    [Fact]
    public void Sync_PushFailure_AfterSuccessfulPull_ReturnsError()
    {
        InsertPerson(_serverDb, "s1", "ServerAlice");
        InsertPerson(_clientDb, "c1", "ClientBob");

        var result = SyncCoordinator.Sync(
            ClientOrigin,
            0,
            0,
            new BatchConfig(100),
            (from, limit) => FetchChanges(_serverDb, from, limit),
            entry => ApplyChange(_clientDb, entry),
            () => EnableSuppression(_clientDb),
            () => DisableSuppression(_clientDb),
            v => SetLastSyncVersion(_clientDb, v),
            (from, limit) => FetchChanges(_clientDb, from, limit),
            changes => new BoolSyncError(new SyncErrorDatabase("Push failed")),
            v => SetLastPushVersion(_clientDb, v),
            Logger
        );

        Assert.IsType<SyncResultError>(result);
        // Pull should have succeeded - client has server's data
        Assert.Equal("ServerAlice", GetPersonName(_clientDb, "s1"));
    }

    [Fact]
    public void Sync_IncrementalSync_OnlyNewChanges()
    {
        // Initial sync
        InsertPerson(_serverDb, "s1", "ServerAlice");
        var result1 = SyncCoordinator.Pull(
            ClientOrigin,
            0,
            new BatchConfig(100),
            (from, limit) => FetchChanges(_serverDb, from, limit),
            entry => ApplyChange(_clientDb, entry),
            () => EnableSuppression(_clientDb),
            () => DisableSuppression(_clientDb),
            v => SetLastSyncVersion(_clientDb, v),
            Logger
        );

        var firstPull = ((PullResultOk)result1).Value;
        Assert.Equal(1, firstPull.ChangesApplied);

        // Add more data on server
        InsertPerson(_serverDb, "s2", "ServerBob");

        // Second sync from last version
        var result2 = SyncCoordinator.Pull(
            ClientOrigin,
            firstPull.ToVersion,
            new BatchConfig(100),
            (from, limit) => FetchChanges(_serverDb, from, limit),
            entry => ApplyChange(_clientDb, entry),
            () => EnableSuppression(_clientDb),
            () => DisableSuppression(_clientDb),
            v => SetLastSyncVersion(_clientDb, v),
            Logger
        );

        var secondPull = ((PullResultOk)result2).Value;
        Assert.Equal(1, secondPull.ChangesApplied); // Only Bob
        Assert.Equal("ServerBob", GetPersonName(_clientDb, "s2"));
    }

    #endregion

    #region Helper Methods

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

            INSERT INTO _sync_state VALUES ('origin_id', '{originId}');
            INSERT INTO _sync_state VALUES ('last_server_version', '0');
            INSERT INTO _sync_state VALUES ('last_push_version', '0');

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

            -- Sync triggers
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

            CREATE TRIGGER Parent_sync_insert AFTER INSERT ON Parent
            WHEN (SELECT sync_active FROM _sync_session) = 0
            BEGIN
                INSERT INTO _sync_log (table_name, pk_value, operation, payload, origin, timestamp)
                VALUES ('Parent', json_object('Id', NEW.Id), 'insert',
                        json_object('Id', NEW.Id, 'Name', NEW.Name),
                        (SELECT value FROM _sync_state WHERE key = 'origin_id'),
                        strftime('%Y-%m-%dT%H:%M:%fZ', 'now'));
            END;

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

    private static SyncLogListResult FetchChanges(SqliteConnection db, long fromVersion, int limit)
    {
        try
        {
            var entries = new List<SyncLogEntry>();
            using var cmd = db.CreateCommand();
            cmd.CommandText = """
                SELECT version, table_name, pk_value, operation, payload, origin, timestamp
                FROM _sync_log
                WHERE version > $fromVersion
                ORDER BY version ASC
                LIMIT $limit
                """;
            cmd.Parameters.AddWithValue("$fromVersion", fromVersion);
            cmd.Parameters.AddWithValue("$limit", limit);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                entries.Add(
                    new SyncLogEntry(
                        reader.GetInt64(0),
                        reader.GetString(1),
                        reader.GetString(2),
                        ParseOperation(reader.GetString(3)),
                        reader.IsDBNull(4) ? null : reader.GetString(4),
                        reader.GetString(5),
                        reader.GetString(6)
                    )
                );
            }

            return new SyncLogListOk(entries);
        }
        catch (SqliteException ex)
        {
            return new SyncLogListError(new SyncErrorDatabase(ex.Message));
        }
    }

    private static BoolSyncResult EnableSuppression(SqliteConnection db)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = "UPDATE _sync_session SET sync_active = 1";
        cmd.ExecuteNonQuery();
        return new BoolSyncOk(true);
    }

    private static BoolSyncResult DisableSuppression(SqliteConnection db)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = "UPDATE _sync_session SET sync_active = 0";
        cmd.ExecuteNonQuery();
        return new BoolSyncOk(true);
    }

    private static BoolSyncResult ApplyChange(SqliteConnection db, SyncLogEntry entry)
    {
        try
        {
            switch (entry.Operation)
            {
                case SyncOperation.Insert:
                case SyncOperation.Update:
                    ApplyUpsert(db, entry);
                    break;
                case SyncOperation.Delete:
                    ApplyDelete(db, entry);
                    break;
            }
            return new BoolSyncOk(true);
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 19)
        {
            return new BoolSyncOk(false); // FK violation - defer
        }
        catch (SqliteException ex)
        {
            return new BoolSyncError(new SyncErrorDatabase(ex.Message));
        }
    }

    private static BoolSyncResult ApplyChangesToTarget(
        SqliteConnection db,
        IReadOnlyList<SyncLogEntry> changes,
        string targetOrigin
    )
    {
        EnableSuppression(db);
        try
        {
            foreach (var entry in changes)
            {
                // Skip echo
                if (entry.Origin == targetOrigin)
                    continue;
                ApplyChange(db, entry);
            }
            return new BoolSyncOk(true);
        }
        finally
        {
            DisableSuppression(db);
        }
    }

    private static void ApplyUpsert(SqliteConnection db, SyncLogEntry entry)
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
                    "INSERT OR REPLACE INTO Child (Id, ParentId, Name) VALUES ($id, $pid, $name)";
                cmd.Parameters.AddWithValue("$id", payload!["Id"].GetString());
                cmd.Parameters.AddWithValue("$pid", payload["ParentId"].GetString());
                cmd.Parameters.AddWithValue("$name", payload["Name"].GetString());
                break;
        }
        cmd.ExecuteNonQuery();
    }

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

    private static void InsertPerson(SqliteConnection db, string id, string name)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = "INSERT INTO Person (Id, Name) VALUES ($id, $name)";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.Parameters.AddWithValue("$name", name);
        cmd.ExecuteNonQuery();
    }

    private static void InsertParent(SqliteConnection db, string id, string name)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = "INSERT INTO Parent (Id, Name) VALUES ($id, $name)";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.Parameters.AddWithValue("$name", name);
        cmd.ExecuteNonQuery();
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

    private static void InsertSyncLogEntry(
        SqliteConnection db,
        string tableName,
        string pkValue,
        string operation,
        string payload,
        string origin
    )
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = """
            INSERT INTO _sync_log (table_name, pk_value, operation, payload, origin, timestamp)
            VALUES ($table, $pk, $op, $payload, $origin, strftime('%Y-%m-%dT%H:%M:%fZ', 'now'))
            """;
        cmd.Parameters.AddWithValue("$table", tableName);
        cmd.Parameters.AddWithValue("$pk", pkValue);
        cmd.Parameters.AddWithValue("$op", operation);
        cmd.Parameters.AddWithValue("$payload", payload);
        cmd.Parameters.AddWithValue("$origin", origin);
        cmd.ExecuteNonQuery();
    }

    private static string? GetName(SqliteConnection db, string table, string id)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = $"SELECT Name FROM {table} WHERE Id = $id";
        cmd.Parameters.AddWithValue("$id", id);
        return cmd.ExecuteScalar() as string;
    }

    private static string? GetPersonName(SqliteConnection db, string id) =>
        GetName(db, "Person", id);

    private static string? GetParentName(SqliteConnection db, string id) =>
        GetName(db, "Parent", id);

    private static string? GetChildName(SqliteConnection db, string id) => GetName(db, "Child", id);

    private static void SetLastSyncVersion(SqliteConnection db, long version)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = "UPDATE _sync_state SET value = $v WHERE key = 'last_server_version'";
        cmd.Parameters.AddWithValue("$v", version.ToString(CultureInfo.InvariantCulture));
        cmd.ExecuteNonQuery();
    }

    private static void SetLastPushVersion(SqliteConnection db, long version)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = "UPDATE _sync_state SET value = $v WHERE key = 'last_push_version'";
        cmd.Parameters.AddWithValue("$v", version.ToString(CultureInfo.InvariantCulture));
        cmd.ExecuteNonQuery();
    }

    private static SyncOperation ParseOperation(string op) =>
        op switch
        {
            "insert" => SyncOperation.Insert,
            "update" => SyncOperation.Update,
            "delete" => SyncOperation.Delete,
            _ => throw new ArgumentException($"Unknown operation: {op}"),
        };

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
