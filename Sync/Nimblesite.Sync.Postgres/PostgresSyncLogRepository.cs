using System.Globalization;

namespace Nimblesite.Sync.Postgres;

/// <summary>
/// Repository for sync log operations in PostgreSQL.
/// Implements spec Section 7 (Unified Change Log) for Postgres.
/// </summary>
public static partial class PostgresSyncLogRepository
{
    /// <summary>
    /// Fetches changes from _sync_log since the specified version.
    /// </summary>
    /// <param name="connection">PostgreSQL connection.</param>
    /// <param name="fromVersion">Version to start from (exclusive).</param>
    /// <param name="limit">Maximum number of changes to return.</param>
    /// <returns>List of changes or database error.</returns>
    public static SyncLogListResult FetchChanges(
        NpgsqlConnection connection,
        long fromVersion,
        int limit
    )
    {
        return SyncCommandExecution.Read<NpgsqlException, SyncLogListResult>(
            connection: connection,
            setSql: command =>
                command.CommandText = global::Nimblesite
                    .Sync
                    .Postgres
                    .PostgresSyncLogRepository
                    .SqlSpecification1,
            parameters: () => [("@fromVersion", fromVersion), ("@limit", limit)],
            read: command =>
            {
                return new SyncLogListOk(
                    SyncCommandExecution.ReadRows<SyncLogEntry>(
                        command: command,
                        read: row => ReadSyncLogEntry(row)
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
    /// Inserts a change into _sync_log.
    /// </summary>
    /// <param name="connection">PostgreSQL connection.</param>
    /// <param name="entry">Entry to insert.</param>
    /// <returns>Success or database error.</returns>
    public static BoolSyncResult Insert(NpgsqlConnection connection, SyncLogEntry entry) =>
        SyncCommandExecution.Write<NpgsqlException>(
            connection: connection,
            setSql: command =>
                command.CommandText = global::Nimblesite
                    .Sync
                    .Postgres
                    .PostgresSyncLogRepository
                    .SqlSpecification2,
            parameters: () =>
                [
                    ("@tableName", entry.TableName),
                    ("@pkValue", entry.PkValue),
                    ("@operation", entry.Operation.ToString().ToLowerInvariant()),
                    ("@payload", entry.Payload ?? (object)DBNull.Value),
                    ("@origin", entry.Origin),
                    ("@timestamp", entry.Timestamp),
                ],
            failurePrefix: "Failed to insert change"
        );

    /// <summary>
    /// Gets the last server version from _sync_state.
    /// </summary>
    /// <param name="connection">PostgreSQL connection.</param>
    /// <returns>Last server version or database error.</returns>
    public static LongSyncResult GetLastServerVersion(NpgsqlConnection connection)
    {
        return SyncCommandExecution.Read<NpgsqlException, LongSyncResult>(
            connection: connection,
            setSql: command =>
                command.CommandText =
                    "SELECT value FROM _sync_state WHERE key = 'last_server_version'",
            parameters: () => [],
            read: command =>
            {
                var result = command.ExecuteScalar();
                return new LongSyncOk(
                    result is not null && long.TryParse(result.ToString(), out var v) ? v : 0
                );
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
    /// Gets the maximum version in _sync_log.
    /// </summary>
    /// <param name="connection">PostgreSQL connection.</param>
    /// <returns>Max version or database error.</returns>
    public static LongSyncResult GetMaxVersion(NpgsqlConnection connection)
    {
        return SyncCommandExecution.Read<NpgsqlException, LongSyncResult>(
            connection: connection,
            setSql: command =>
                command.CommandText = "SELECT COALESCE(MAX(version), 0) FROM _sync_log",
            parameters: () => [],
            read: command =>
            {
                var result = command.ExecuteScalar();
                return new LongSyncOk(Convert.ToInt64(result, CultureInfo.InvariantCulture));
            },
            failure: ex =>
            {
                return new LongSyncError(
                    new SyncErrorDatabase($"Failed to get max version: {ex.Message}")
                );
            }
        );
    }

    /// <summary>
    /// Updates the last server version in _sync_state.
    /// </summary>
    /// <param name="connection">PostgreSQL connection.</param>
    /// <param name="version">Version to set.</param>
    /// <returns>Success or database error.</returns>
    public static BoolSyncResult UpdateLastServerVersion(
        NpgsqlConnection connection,
        long version
    ) =>
        SyncCommandExecution.Write<NpgsqlException>(
            connection: connection,
            setSql: command =>
                command.CommandText = global::Nimblesite
                    .Sync
                    .Postgres
                    .PostgresSyncLogRepository
                    .SqlSpecification3,
            parameters: () => [("@version", version.ToString(CultureInfo.InvariantCulture))],
            failurePrefix: "Failed to update last server version"
        );

    private static SyncLogEntry ReadSyncLogEntry(global::System.Data.Common.DbDataReader reader) =>
        new(
            Version: reader.GetInt64(0),
            TableName: reader.GetString(1),
            PkValue: reader.GetString(2),
            Operation: ParseOperation(reader.GetString(3)),
            Payload: reader.IsDBNull(4) ? null : reader.GetString(4),
            Origin: reader.GetString(5),
            Timestamp: reader.GetString(6)
        );

    private static SyncOperation ParseOperation(string op) =>
        op.ToLowerInvariant() switch
        {
            "insert" => SyncOperation.Insert,
            "update" => SyncOperation.Update,
            "delete" => SyncOperation.Delete,
            _ => SyncOperation.Update,
        };
}
