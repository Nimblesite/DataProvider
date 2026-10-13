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
    public const string CreateSyncStateTable = global::Nimblesite
        .Sync
        .SQLite
        .MappingStateRepository
        .SqlSpecification14;

    /// <summary>
    /// SQL to create sync session table (_sync_session).
    /// Single row with sync_active flag for trigger suppression.
    /// </summary>
    public const string CreateSyncSessionTable = global::Nimblesite
        .Sync
        .SQLite
        .MappingStateRepository
        .SqlSpecification15;

    /// <summary>
    /// SQL to create sync log table (_sync_log).
    /// Unified change log with JSON payloads.
    /// </summary>
    public const string CreateSyncLogTable = global::Nimblesite
        .Sync
        .SQLite
        .MappingStateRepository
        .SqlSpecification16;

    /// <summary>
    /// SQL to create sync clients table (_sync_clients).
    /// Server-side tracking of client sync state for tombstone retention.
    /// Maps to spec Section 13.3 / Appendix B.
    /// </summary>
    public const string CreateSyncClientsTable = global::Nimblesite
        .Sync
        .SQLite
        .MappingStateRepository
        .SqlSpecification17;

    /// <summary>
    /// SQL to create sync subscriptions table (_sync_subscriptions).
    /// Server-side tracking of real-time subscriptions.
    /// Maps to spec Section 10.6.
    /// </summary>
    public const string CreateSyncSubscriptionsTable = global::Nimblesite
        .Sync
        .SQLite
        .MappingStateRepository
        .SqlSpecification18;

    /// <summary>
    /// SQL to create mapping state table (_sync_mapping_state).
    /// Per-mapping sync state for version-based tracking.
    /// Maps to spec Section 7.5.2.
    /// </summary>
    public const string CreateMappingStateTable = global::Nimblesite
        .Sync
        .SQLite
        .MappingStateRepository
        .SqlSpecification19;

    /// <summary>
    /// SQL to create record hashes table (_sync_record_hashes).
    /// Per-record hash tracking for hash-based sync strategy.
    /// Maps to spec Section 7.5.2.
    /// </summary>
    public const string CreateRecordHashesTable = global::Nimblesite
        .Sync
        .SQLite
        .MappingStateRepository
        .SqlSpecification20;

    /// <summary>
    /// SQL to initialize sync state with default values.
    /// </summary>
    public const string InitializeSyncState = global::Nimblesite
        .Sync
        .SQLite
        .MappingStateRepository
        .SqlSpecification21;

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
        return SyncCommandExecution.Read<SqliteException, StringSyncResult>(
            connection: connection,
            setSql: command =>
                command.CommandText = "SELECT value FROM _sync_state WHERE key = 'origin_id'",
            parameters: () => [],
            read: command =>
            {
                var result = command.ExecuteScalar();
                return result is string originId
                    ? new StringSyncOk(originId)
                    : new StringSyncError(new SyncErrorDatabase("Origin ID not found"));
            },
            failure: ex =>
            {
                return new StringSyncError(
                    new SyncErrorDatabase($"Failed to get origin ID: {ex.Message}")
                );
            }
        );
    }
}
