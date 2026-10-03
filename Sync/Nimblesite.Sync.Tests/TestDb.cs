using Microsoft.Data.Sqlite;
using Outcome;

namespace Nimblesite.Sync.Tests;

/// <summary>
/// File-based SQLite database for integration testing.
/// </summary>
public sealed class TestDb : IDisposable
{
    private readonly string _dbPath = Path.Combine(
        Path.GetTempPath(),
        $"testdb_{Guid.NewGuid()}.db"
    );

    public SqliteConnection Connection { get; }

    public TestDb()
    {
        Connection = new SqliteConnection($"Data Source={_dbPath}");
        Connection.Open();
        InitializeSyncSchema();
    }

    private void InitializeSyncSchema()
    {
        using var cmd = Connection.CreateCommand();
        cmd.CommandText = """
            -- Sync state (persistent)
            CREATE TABLE _sync_state (
                key TEXT PRIMARY KEY,
                value TEXT NOT NULL
            );

            -- Sync session (ephemeral flag)
            CREATE TABLE _sync_session (
                sync_active INTEGER DEFAULT 0
            );
            INSERT INTO _sync_session VALUES (0);

            -- Change log
            CREATE TABLE _sync_log (
                version     INTEGER PRIMARY KEY AUTOINCREMENT,
                table_name  TEXT NOT NULL,
                pk_value    TEXT NOT NULL,
                operation   TEXT NOT NULL CHECK (operation IN ('insert', 'update', 'delete')),
                payload     TEXT,
                origin      TEXT NOT NULL,
                timestamp   TEXT NOT NULL
            );

            CREATE INDEX idx_sync_log_version ON _sync_log(version);
            CREATE INDEX idx_sync_log_table ON _sync_log(table_name, version);

            -- Initialize state
            INSERT INTO _sync_state VALUES ('origin_id', 'test-origin-001');
            INSERT INTO _sync_state VALUES ('last_server_version', '0');
            INSERT INTO _sync_state VALUES ('last_push_version', '0');

            -- Test table with FK for testing defer/retry
            CREATE TABLE Parent (
                Id TEXT PRIMARY KEY,
                Name TEXT NOT NULL
            );

            CREATE TABLE Child (
                Id TEXT PRIMARY KEY,
                ParentId TEXT NOT NULL REFERENCES Parent(Id),
                Name TEXT NOT NULL
            );
            """;
        cmd.ExecuteNonQuery();
    }

    public void InsertSyncLogEntry(
        string tableName,
        string pkValue,
        string operation,
        string? payload,
        string origin,
        string timestamp
    )
    {
        using var cmd = Connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO _sync_log (table_name, pk_value, operation, payload, origin, timestamp)
            VALUES ($tableName, $pkValue, $operation, $payload, $origin, $timestamp)
            """;
        cmd.Parameters.AddWithValue("$tableName", tableName);
        cmd.Parameters.AddWithValue("$pkValue", pkValue);
        cmd.Parameters.AddWithValue("$operation", operation);
        cmd.Parameters.AddWithValue("$payload", payload ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$origin", origin);
        cmd.Parameters.AddWithValue("$timestamp", timestamp);
        cmd.ExecuteNonQuery();
    }

    public IReadOnlyList<SyncLogEntry> FetchChanges(long fromVersion, int limit)
    {
        var entries = new List<SyncLogEntry>();
        using var cmd = Connection.CreateCommand();
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

        return entries;
    }

    private static SyncOperation ParseOperation(string op) =>
        op switch
        {
            "insert" => SyncOperation.Insert,
            "update" => SyncOperation.Update,
            "delete" => SyncOperation.Delete,
            _ => throw new ArgumentException($"Unknown operation: {op}"),
        };

    internal static T AssertSuccess<T>(Result<T, SyncError> result)
    {
        Assert.IsType<Result<T, SyncError>.Ok<T, SyncError>>(result);
        return ((Result<T, SyncError>.Ok<T, SyncError>)result).Value;
    }

    public void Dispose()
    {
        Connection.Dispose();
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
