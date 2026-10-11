namespace Nimblesite.Sync.Postgres;

/// <summary>
/// Repository for managing sync clients in PostgreSQL.
/// Implements spec Section 13 (Tombstone Retention) client tracking.
/// </summary>
public static class PostgresSyncClientRepository
{
    /// <summary>
    /// Gets all sync clients from _sync_clients table.
    /// </summary>
    /// <param name="connection">PostgreSQL connection.</param>
    /// <returns>List of sync clients or database error.</returns>
    public static SyncClientListResult GetAll(NpgsqlConnection connection) =>
        SyncClientStorage.GetAll<NpgsqlException>(
            connection: connection,
            failurePrefix: "Failed to get sync clients"
        );

    /// <summary>
    /// Gets a sync client by origin ID.
    /// </summary>
    /// <param name="connection">PostgreSQL connection.</param>
    /// <param name="originId">Origin ID to look up.</param>
    /// <returns>Sync client if found, null if not found, or database error.</returns>
    public static SyncClientResult GetByOrigin(NpgsqlConnection connection, string originId) =>
        SyncClientStorage.GetByOrigin<NpgsqlException>(connection: connection, originId: originId);

    /// <summary>
    /// Upserts a sync client record.
    /// </summary>
    /// <param name="connection">PostgreSQL connection.</param>
    /// <param name="client">Client to upsert.</param>
    /// <returns>Success or database error.</returns>
    public static BoolSyncResult Upsert(NpgsqlConnection connection, SyncClient client) =>
        SyncClientStorage.Upsert<NpgsqlException>(
            connection: connection,
            client: client,
            failurePrefix: "Failed to upsert sync client"
        );

    /// <summary>
    /// Deletes a sync client by origin ID.
    /// </summary>
    /// <param name="connection">PostgreSQL connection.</param>
    /// <param name="originId">Origin ID to delete.</param>
    /// <returns>Success or database error.</returns>
    public static BoolSyncResult Delete(NpgsqlConnection connection, string originId) =>
        SyncClientStorage.Delete<NpgsqlException>(connection: connection, originId: originId);

    /// <summary>
    /// Gets the minimum sync version across all clients.
    /// </summary>
    /// <param name="connection">PostgreSQL connection.</param>
    /// <returns>Minimum version or 0 if no clients.</returns>
    public static LongSyncResult GetMinVersion(NpgsqlConnection connection)
    {
        try
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT COALESCE(MIN(last_sync_version), 0) FROM _sync_clients";

            var result = cmd.ExecuteScalar();
            var version = result is long v ? v : 0;

            return new LongSyncOk(version);
        }
        catch (NpgsqlException ex)
        {
            return new LongSyncError(
                new SyncErrorDatabase($"Failed to get minimum sync version: {ex.Message}")
            );
        }
    }
}
