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
    public static SyncClientListResult GetAll(NpgsqlConnection connection)
    {
        try
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = """
                SELECT origin_id, last_sync_version, last_sync_timestamp, created_at
                FROM _sync_clients
                ORDER BY last_sync_version ASC
                """;

            var clients = new List<SyncClient>();
            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                clients.Add(MapClient(reader));
            }

            return new SyncClientListOk(clients);
        }
        catch (NpgsqlException ex)
        {
            return new SyncClientListError(
                new SyncErrorDatabase($"Failed to get sync clients: {ex.Message}")
            );
        }
    }

    /// <summary>
    /// Gets a sync client by origin ID.
    /// </summary>
    /// <param name="connection">PostgreSQL connection.</param>
    /// <param name="originId">Origin ID to look up.</param>
    /// <returns>Sync client if found, null if not found, or database error.</returns>
    public static SyncClientResult GetByOrigin(NpgsqlConnection connection, string originId)
    {
        try
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = """
                SELECT origin_id, last_sync_version, last_sync_timestamp, created_at
                FROM _sync_clients
                WHERE origin_id = @originId
                """;
            cmd.Parameters.AddWithValue("@originId", originId);

            using var reader = cmd.ExecuteReader();

            if (!reader.Read())
            {
                return new SyncClientOk(null);
            }

            var client = MapClient(reader);

            return new SyncClientOk(client);
        }
        catch (NpgsqlException ex)
        {
            return new SyncClientError(
                new SyncErrorDatabase($"Failed to get sync client: {ex.Message}")
            );
        }
    }

    /// <summary>
    /// Upserts a sync client record.
    /// </summary>
    /// <param name="connection">PostgreSQL connection.</param>
    /// <param name="client">Client to upsert.</param>
    /// <returns>Success or database error.</returns>
    public static BoolSyncResult Upsert(NpgsqlConnection connection, SyncClient client)
    {
        try
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = """
                INSERT INTO _sync_clients (origin_id, last_sync_version, last_sync_timestamp, created_at)
                VALUES (@originId, @lastVersion, @lastTimestamp, @createdAt)
                ON CONFLICT(origin_id) DO UPDATE SET
                    last_sync_version = @lastVersion,
                    last_sync_timestamp = @lastTimestamp
                """;
            cmd.Parameters.AddWithValue("@originId", client.OriginId);
            cmd.Parameters.AddWithValue("@lastVersion", client.LastSyncVersion);
            cmd.Parameters.AddWithValue("@lastTimestamp", client.LastSyncTimestamp);
            cmd.Parameters.AddWithValue("@createdAt", client.CreatedAt);

            cmd.ExecuteNonQuery();
            return new BoolSyncOk(true);
        }
        catch (NpgsqlException ex)
        {
            return new BoolSyncError(
                new SyncErrorDatabase($"Failed to upsert sync client: {ex.Message}")
            );
        }
    }

    /// <summary>
    /// Deletes a sync client by origin ID.
    /// </summary>
    /// <param name="connection">PostgreSQL connection.</param>
    /// <param name="originId">Origin ID to delete.</param>
    /// <returns>Success or database error.</returns>
    public static BoolSyncResult Delete(NpgsqlConnection connection, string originId)
    {
        try
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "DELETE FROM _sync_clients WHERE origin_id = @originId";
            cmd.Parameters.AddWithValue("@originId", originId);
            cmd.ExecuteNonQuery();
            return new BoolSyncOk(true);
        }
        catch (NpgsqlException ex)
        {
            return new BoolSyncError(
                new SyncErrorDatabase($"Failed to delete sync client: {ex.Message}")
            );
        }
    }

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

    private static SyncClient MapClient(NpgsqlDataReader reader) =>
        new(
            OriginId: reader.GetString(0),
            LastSyncVersion: reader.GetInt64(1),
            LastSyncTimestamp: reader.GetString(2),
            CreatedAt: reader.GetString(3)
        );
}
