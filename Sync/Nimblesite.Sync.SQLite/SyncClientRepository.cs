using Microsoft.Data.Sqlite;

namespace Nimblesite.Sync.SQLite;

// Implements [SYNC-SCALAR-READ-SHARED].

/// <summary>
/// Repository for managing sync clients in SQLite.
/// Implements spec Section 13 (Tombstone Retention) client tracking.
/// </summary>
public static class SyncClientRepository
{
    /// <summary>
    /// Gets all sync clients from _sync_clients table.
    /// </summary>
    /// <param name="connection">SQLite connection.</param>
    /// <returns>List of sync clients or database error.</returns>
    public static SyncClientListResult GetAll(SqliteConnection connection) =>
        SyncClientStorage.GetAll<SqliteException>(
            connection: connection,
            failurePrefix: "Failed to get sync clients"
        );

    // Implements [SYNC-CLIENT-API-SHARED].
    internal static SyncClientListResult GetAll(
        SqliteConnection connection,
        string failurePrefix
    ) =>
        SyncClientStorage.GetAll<SqliteException>(
            connection: connection,
            failurePrefix: failurePrefix
        );

    /// <summary>
    /// Gets a sync client by origin ID.
    /// </summary>
    /// <param name="connection">SQLite connection.</param>
    /// <param name="originId">Origin ID to look up.</param>
    /// <returns>Sync client if found, null if not found, or database error.</returns>
    public static SyncClientResult GetByOrigin(SqliteConnection connection, string originId) =>
        SyncClientStorage.GetByOrigin<SqliteException>(connection: connection, originId: originId);

    /// <summary>
    /// Upserts a sync client record.
    /// Inserts if not exists, updates if exists.
    /// </summary>
    /// <param name="connection">SQLite connection.</param>
    /// <param name="client">Client to upsert.</param>
    /// <returns>Success or database error.</returns>
    public static BoolSyncResult Upsert(SqliteConnection connection, SyncClient client) =>
        SyncClientStorage.Upsert<SqliteException>(
            connection: connection,
            client: client,
            failurePrefix: "Failed to upsert sync client"
        );

    internal static BoolSyncResult Upsert(
        SqliteConnection connection,
        SyncClient client,
        string failurePrefix
    ) =>
        SyncClientStorage.Upsert<SqliteException>(
            connection: connection,
            client: client,
            failurePrefix: failurePrefix
        );

    /// <summary>
    /// Deletes a sync client by origin ID.
    /// </summary>
    /// <param name="connection">SQLite connection.</param>
    /// <param name="originId">Origin ID to delete.</param>
    /// <returns>Success or database error.</returns>
    public static BoolSyncResult Delete(SqliteConnection connection, string originId) =>
        SyncClientStorage.Delete<SqliteException>(connection: connection, originId: originId);

    /// <summary>
    /// Gets the minimum sync version across all clients.
    /// Used for tombstone retention calculation (spec Section 13.4).
    /// </summary>
    /// <param name="connection">SQLite connection.</param>
    /// <returns>Minimum version or 0 if no clients, or database error.</returns>
    public static LongSyncResult GetMinVersion(SqliteConnection connection) =>
        SqliteCommandExecution.ReadInt64OrZero(
            connection: connection,
            setSql: command =>
                command.CommandText = "SELECT MIN(last_sync_version) FROM _sync_clients",
            failurePrefix: "Failed to get minimum sync version"
        );

    /// <summary>
    /// Deletes multiple sync clients by origin IDs.
    /// Used to clean up stale clients (spec Section 13.5).
    /// </summary>
    /// <param name="connection">SQLite connection.</param>
    /// <param name="originIds">Origin IDs to delete.</param>
    /// <returns>Number of deleted clients or database error.</returns>
    public static IntSyncResult DeleteMultiple(
        SqliteConnection connection,
        IEnumerable<string> originIds
    ) =>
        SyncClientStorage.DeleteMultiple<SqliteException>(
            connection: connection,
            originIds: originIds,
            failurePrefix: "Failed to delete sync clients"
        );

    internal static IntSyncResult DeleteMultiple(
        SqliteConnection connection,
        IEnumerable<string> originIds,
        string failurePrefix
    ) =>
        SyncClientStorage.DeleteMultiple<SqliteException>(
            connection: connection,
            originIds: originIds,
            failurePrefix: failurePrefix
        );
}
