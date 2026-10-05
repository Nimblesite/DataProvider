using System.Globalization;
using Microsoft.Data.Sqlite;

namespace Nimblesite.Sync.SQLite;

// Implements [SYNC-SCALAR-READ-SHARED].

/// <summary>
/// Static methods for sync log operations.
/// FP-style - no instance state, pure functions.
/// Implements spec Section 7 (Unified Change Log) and Section 12 (Batching).
/// </summary>
public static class SyncLogRepository
{
    /// <summary>
    /// Fetches a batch of changes from the sync log.
    /// </summary>
    /// <param name="connection">SQLite connection.</param>
    /// <param name="fromVersion">Fetch changes with version greater than this.</param>
    /// <param name="batchSize">Maximum number of changes to fetch.</param>
    /// <returns>List of sync log entries or database error.</returns>
    public static SyncLogListResult FetchChanges(
        SqliteConnection connection,
        long fromVersion,
        int batchSize
    )
    {
        try
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = """
                SELECT version, table_name, pk_value, operation, payload, origin, timestamp
                FROM _sync_log
                WHERE version > @fromVersion
                ORDER BY version ASC
                LIMIT @batchSize
                """;
            cmd.Parameters.AddWithValue("@fromVersion", fromVersion);
            cmd.Parameters.AddWithValue("@batchSize", batchSize);

            var entries = new List<SyncLogEntry>();
            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                entries.Add(
                    new SyncLogEntry(
                        Version: reader.GetInt64(0),
                        TableName: reader.GetString(1),
                        PkValue: reader.GetString(2),
                        Operation: ParseOperation(reader.GetString(3)),
                        Payload: reader.IsDBNull(4) ? null : reader.GetString(4),
                        Origin: reader.GetString(5),
                        Timestamp: reader.GetString(6)
                    )
                );
            }

            return new SyncLogListOk(entries);
        }
        catch (SqliteException ex)
        {
            return new SyncLogListError(
                new SyncErrorDatabase($"Failed to fetch changes: {ex.Message}")
            );
        }
    }

    /// <summary>
    /// Gets the last server version from sync state.
    /// </summary>
    /// <param name="connection">SQLite connection.</param>
    /// <returns>Last server version or database error.</returns>
    public static LongSyncResult GetLastServerVersion(SqliteConnection connection)
    {
        try
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT value FROM _sync_state WHERE key = 'last_server_version'";
            var result = cmd.ExecuteScalar();
            return result is string strValue && long.TryParse(strValue, out var version)
                ? new LongSyncOk(version)
                : new LongSyncOk(0);
        }
        catch (SqliteException ex)
        {
            return new LongSyncError(
                new SyncErrorDatabase($"Failed to get last server version: {ex.Message}")
            );
        }
    }

    /// <summary>
    /// Updates the last server version in sync state.
    /// </summary>
    /// <param name="connection">SQLite connection.</param>
    /// <param name="version">New last server version.</param>
    /// <returns>Success or database error.</returns>
    public static BoolSyncResult UpdateLastServerVersion(SqliteConnection connection, long version)
    {
        try
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText =
                "UPDATE _sync_state SET value = @version WHERE key = 'last_server_version'";
            cmd.Parameters.AddWithValue("@version", version.ToString(CultureInfo.InvariantCulture));
            cmd.ExecuteNonQuery();
            return new BoolSyncOk(true);
        }
        catch (SqliteException ex)
        {
            return new BoolSyncError(
                new SyncErrorDatabase($"Failed to update last server version: {ex.Message}")
            );
        }
    }

    /// <summary>
    /// Gets the maximum version in the sync log.
    /// </summary>
    /// <param name="connection">SQLite connection.</param>
    /// <returns>Max version or 0 if no entries.</returns>
    public static LongSyncResult GetMaxVersion(SqliteConnection connection) =>
        SqliteCommandExecution.ReadInt64OrZero(
            connection: connection,
            setSql: command => command.CommandText = "SELECT MAX(version) FROM _sync_log",
            failurePrefix: "Failed to get max version"
        );

    /// <summary>
    /// Gets the minimum version in the sync log.
    /// Used for tombstone retention checks (spec Section 13.6).
    /// </summary>
    /// <param name="connection">SQLite connection.</param>
    /// <returns>Min version or 0 if no entries.</returns>
    public static LongSyncResult GetMinVersion(SqliteConnection connection) =>
        SqliteCommandExecution.ReadInt64OrZero(
            connection: connection,
            setSql: command => command.CommandText = "SELECT MIN(version) FROM _sync_log",
            failurePrefix: "Failed to get min version"
        );

    /// <summary>
    /// Gets total count of entries in the sync log.
    /// </summary>
    /// <param name="connection">SQLite connection.</param>
    /// <returns>Count or database error.</returns>
    public static LongSyncResult GetEntryCount(SqliteConnection connection) =>
        SqliteCommandExecution.ReadInt64OrZero(
            connection: connection,
            setSql: command => command.CommandText = "SELECT COUNT(*) FROM _sync_log",
            failurePrefix: "Failed to get entry count"
        );

    private static SyncOperation ParseOperation(string operation) =>
        operation.ToLowerInvariant() switch
        {
            "insert" => SyncOperation.Insert,
            "update" => SyncOperation.Update,
            "delete" => SyncOperation.Delete,
            _ => SyncOperation.Insert,
        };
}
