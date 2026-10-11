using System.Diagnostics.CodeAnalysis;
using Microsoft.Data.Sqlite;

namespace Nimblesite.Sync.SQLite;

/// <summary>
/// Creates and manages sync schema tables for SQLite.
/// Implements spec Appendix A schema.
/// </summary>
public static class SyncSchema
{
    /// <summary>
    /// SQL to create sync state table (_sync_state).
    /// Stores origin ID and version tracking.
    /// </summary>
    public const string CreateSyncStateTable = """
        CREATE TABLE IF NOT EXISTS _sync_state (
            key TEXT PRIMARY KEY,
            value TEXT NOT NULL
        );
        """;

    /// <summary>
    /// SQL to create sync session table (_sync_session).
    /// Single row with sync_active flag for trigger suppression.
    /// </summary>
    public const string CreateSyncSessionTable = """
        CREATE TABLE IF NOT EXISTS _sync_session (
            sync_active INTEGER DEFAULT 0
        );
        """;

    /// <summary>
    /// SQL to create sync log table (_sync_log).
    /// Unified change log with JSON payloads.
    /// </summary>
    public const string CreateSyncLogTable = """
        CREATE TABLE IF NOT EXISTS _sync_log (
            version     INTEGER PRIMARY KEY AUTOINCREMENT,
            table_name  TEXT NOT NULL,
            pk_value    TEXT NOT NULL,
            operation   TEXT NOT NULL CHECK (operation IN ('insert', 'update', 'delete')),
            payload     TEXT,
            origin      TEXT NOT NULL,
            timestamp   TEXT NOT NULL
        );

        CREATE INDEX IF NOT EXISTS idx_sync_log_version ON _sync_log(version);
        CREATE INDEX IF NOT EXISTS idx_sync_log_table ON _sync_log(table_name, version);
        """;

    /// <summary>
    /// SQL to create sync clients table (_sync_clients).
    /// Server-side tracking of client sync state for tombstone retention.
    /// Maps to spec Section 13.3 / Appendix B.
    /// </summary>
    public const string CreateSyncClientsTable = """
        CREATE TABLE IF NOT EXISTS _sync_clients (
            origin_id TEXT PRIMARY KEY,
            last_sync_version INTEGER NOT NULL DEFAULT 0,
            last_sync_timestamp TEXT NOT NULL,
            created_at TEXT NOT NULL
        );

        CREATE INDEX IF NOT EXISTS idx_sync_clients_version ON _sync_clients(last_sync_version);
        """;

    /// <summary>
    /// SQL to create sync subscriptions table (_sync_subscriptions).
    /// Server-side tracking of real-time subscriptions.
    /// Maps to spec Section 10.6.
    /// </summary>
    public const string CreateSyncSubscriptionsTable = """
        CREATE TABLE IF NOT EXISTS _sync_subscriptions (
            subscription_id TEXT PRIMARY KEY,
            origin_id TEXT NOT NULL,
            subscription_type TEXT NOT NULL CHECK (subscription_type IN ('record', 'table', 'query')),
            table_name TEXT NOT NULL,
            filter TEXT,
            created_at TEXT NOT NULL,
            expires_at TEXT
        );

        CREATE INDEX IF NOT EXISTS idx_subscriptions_table ON _sync_subscriptions(table_name);
        CREATE INDEX IF NOT EXISTS idx_subscriptions_origin ON _sync_subscriptions(origin_id);
        """;

    /// <summary>
    /// SQL to create mapping state table (_sync_mapping_state).
    /// Per-mapping sync state for version-based tracking.
    /// Maps to spec Section 7.5.2.
    /// </summary>
    public const string CreateMappingStateTable = """
        CREATE TABLE IF NOT EXISTS _sync_mapping_state (
            mapping_id TEXT PRIMARY KEY,
            last_synced_version INTEGER NOT NULL DEFAULT 0,
            last_sync_timestamp TEXT NOT NULL,
            records_synced INTEGER NOT NULL DEFAULT 0
        );
        """;

    /// <summary>
    /// SQL to create record hashes table (_sync_record_hashes).
    /// Per-record hash tracking for hash-based sync strategy.
    /// Maps to spec Section 7.5.2.
    /// </summary>
    public const string CreateRecordHashesTable = """
        CREATE TABLE IF NOT EXISTS _sync_record_hashes (
            mapping_id TEXT NOT NULL,
            source_pk TEXT NOT NULL,
            payload_hash TEXT NOT NULL,
            synced_at TEXT NOT NULL,
            PRIMARY KEY (mapping_id, source_pk)
        );

        CREATE INDEX IF NOT EXISTS idx_record_hashes_mapping ON _sync_record_hashes(mapping_id);
        """;

    /// <summary>
    /// SQL to initialize sync state with default values.
    /// </summary>
    public const string InitializeSyncState = """
        INSERT OR IGNORE INTO _sync_state (key, value) VALUES ('origin_id', '');
        INSERT OR IGNORE INTO _sync_state (key, value) VALUES ('last_server_version', '0');
        INSERT OR IGNORE INTO _sync_state (key, value) VALUES ('last_push_version', '0');
        INSERT OR IGNORE INTO _sync_session (sync_active) VALUES (0);
        """;

    /// <summary>
    /// Creates all sync tables in the database.
    /// </summary>
    /// <param name="connection">SQLite connection.</param>
    /// <returns>Success or database error.</returns>
    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "SQL is composed from const strings only"
    )]
    public static BoolSyncResult CreateSchema(SqliteConnection connection) =>
        SyncCommandExecution.Write<SqliteException>(
            connection: connection,
            setSql: command =>
                command.CommandText = string.Join(
                    "\n",
                    CreateSyncStateTable,
                    CreateSyncSessionTable,
                    CreateSyncLogTable,
                    CreateSyncClientsTable,
                    CreateSyncSubscriptionsTable,
                    CreateMappingStateTable,
                    CreateRecordHashesTable,
                    InitializeSyncState
                ),
            parameters: () => [],
            failurePrefix: "Failed to create sync schema"
        );

    /// <summary>
    /// Sets the origin ID for this replica. Should be called once on first sync.
    /// </summary>
    /// <param name="connection">SQLite connection.</param>
    /// <param name="originId">UUID v4 origin ID.</param>
    /// <returns>Success or database error.</returns>
    public static BoolSyncResult SetOriginId(SqliteConnection connection, string originId) =>
        SyncCommandExecution.Write<SqliteException>(
            connection: connection,
            setSql: command =>
                command.CommandText =
                    "UPDATE _sync_state SET value = @origin WHERE key = 'origin_id'",
            parameters: () => [("@origin", originId)],
            failurePrefix: "Failed to set origin ID"
        );

    /// <summary>
    /// Gets the origin ID for this replica.
    /// </summary>
    /// <param name="connection">SQLite connection.</param>
    /// <returns>Origin ID or database error.</returns>
    public static StringSyncResult GetOriginId(SqliteConnection connection)
    {
        try
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT value FROM _sync_state WHERE key = 'origin_id'";
            var result = cmd.ExecuteScalar();
            return result is string originId
                ? new StringSyncOk(originId)
                : new StringSyncError(new SyncErrorDatabase("Origin ID not found"));
        }
        catch (SqliteException ex)
        {
            return new StringSyncError(
                new SyncErrorDatabase($"Failed to get origin ID: {ex.Message}")
            );
        }
    }
}
