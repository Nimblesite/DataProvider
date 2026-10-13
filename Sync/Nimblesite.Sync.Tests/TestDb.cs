// Implements [CI-TEST-SHARED-COMMAND-PARAMETERS].
// Implements [CI-TEST-SHARED-COMMAND].
using Microsoft.Data.Sqlite;
using Outcome;
using Tests.Shared;
using static global::Tests.Shared.ResultAssertions;

namespace Nimblesite.Sync.Tests;

/// <summary>
/// File-based SQLite database for integration testing.
/// </summary>
public sealed partial class TestDb : IDisposable
{
    private readonly string _dbPath = global::Tests.Shared.TempSqliteDatabase.PathFor("testdb");

    public SqliteConnection Connection { get; }

    public TestDb()
    {
        Connection = new SqliteConnection($"Data Source={_dbPath}");
        Connection.Open();
        InitializeSyncSchema();
    }

    private void InitializeSyncSchema()
    {
        DatabaseCommands.Execute(
            connection: Connection,
            configure: command => command.CommandText = Data_InitializeSyncSchema_1
        );
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
        DatabaseCommands.Execute(
            connection: Connection,
            configure: cmd =>
                cmd.CommandText = """
                INSERT INTO _sync_log (table_name, pk_value, operation, payload, origin, timestamp)
                VALUES ($tableName, $pkValue, $operation, $payload, $origin, $timestamp)
                """,
            parameters: () =>
                [
                    ("$tableName", tableName),
                    ("$pkValue", pkValue),
                    ("$operation", operation),
                    ("$payload", payload ?? (object)DBNull.Value),
                    ("$origin", origin),
                    ("$timestamp", timestamp),
                ]
        );
    }

    public IReadOnlyList<SyncLogEntry> FetchChanges(long fromVersion, int limit) =>
        FetchChanges(db: Connection, fromVersion: fromVersion, limit: limit);

    // Implements [SYNC-TEST-LOG-READER].
    internal static IReadOnlyList<SyncLogEntry> FetchChanges(
        SqliteConnection db,
        long fromVersion,
        int limit
    )
    {
        var entries = new List<SyncLogEntry>();
        using var cmd = db.CreateCommand();
        cmd.CommandText = Data_FetchChanges_1;
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
        global::Tests.Shared.ResultAssertions.ExactOk(actual: result);
        return (Ok(actual: result)).Value;
    }

    public void Dispose()
    {
        Connection.Dispose();
        global::Tests.Shared.TempSqliteDatabase.DeleteIfExists(path: _dbPath);
    }
}
