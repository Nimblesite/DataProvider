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
        return SyncCommandExecution.Read<SqliteException, SyncLogListResult>(
            connection: connection,
            setSql: command =>
                command.CommandText = global::Nimblesite
                    .Sync
                    .SQLite
                    .MappingStateRepository
                    .SqlSpecification24,
            parameters: () => [("@fromVersion", fromVersion), ("@batchSize", batchSize)],
            read: command =>
            {
                return new SyncLogListOk(
                    SyncCommandExecution.ReadRows<SyncLogEntry>(
                        command: command,
                        read: row => new SyncLogEntry(
                            Version: row.GetInt64(0),
                            TableName: row.GetString(1),
                            PkValue: row.GetString(2),
                            Operation: ParseOperation(row.GetString(3)),
                            Payload: row.IsDBNull(4) ? null : row.GetString(4),
                            Origin: row.GetString(5),
                            Timestamp: row.GetString(6)
                        )
                    )
                );
            },
            failure: ex =>
            {
                return new SyncLogListError(
                    new SyncErrorDatabase($"Failed to fetch changes: {ex.Message}")
                );
            }
        );
    }

    /// <summary>
    /// Gets the last server version from sync state.
    /// </summary>
    /// <param name="connection">SQLite connection.</param>
    /// <returns>Last server version or database error.</returns>
    public static LongSyncResult GetLastServerVersion(SqliteConnection connection)
    {
        return SyncCommandExecution.Read<SqliteException, LongSyncResult>(
            connection: connection,
            setSql: command =>
                command.CommandText =
                    "SELECT value FROM _sync_state WHERE key = 'last_server_version'",
            parameters: () => [],
            read: command =>
            {
                var result = command.ExecuteScalar();
                return result is string strValue && long.TryParse(strValue, out var version)
                    ? new LongSyncOk(version)
                    : new LongSyncOk(0);
            },
            failure: ex =>
            {
                return new LongSyncError(
                    new SyncErrorDatabase($"Failed to get last server version: {ex.Message}")
                );
            }
        );
    }

    /// <summary>
    /// Updates the last server version in sync state.
    /// </summary>
    /// <param name="connection">SQLite connection.</param>
    /// <param name="version">New last server version.</param>
    /// <returns>Success or database error.</returns>
    public static BoolSyncResult UpdateLastServerVersion(
        SqliteConnection connection,
        long version
    ) =>
        SyncCommandExecution.Write<SqliteException>(
            connection: connection,
            setSql: command =>
                command.CommandText =
                    "UPDATE _sync_state SET value = @version WHERE key = 'last_server_version'",
            parameters: () => [("@version", version.ToString(CultureInfo.InvariantCulture))],
            failurePrefix: "Failed to update last server version"
        );

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
