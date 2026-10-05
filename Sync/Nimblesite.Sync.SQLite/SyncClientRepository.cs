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
        GetAll(connection: connection, failurePrefix: "Failed to get sync clients");

    // Implements [SYNC-CLIENT-API-SHARED].
    internal static SyncClientListResult GetAll(SqliteConnection connection, string failurePrefix)
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
                clients.Add(ReadClient(reader: reader));
            }

            return new SyncClientListOk(clients);
        }
        catch (SqliteException ex)
        {
            return new SyncClientListError(new SyncErrorDatabase($"{failurePrefix}: {ex.Message}"));
        }
    }

    /// <summary>
    /// Gets a sync client by origin ID.
    /// </summary>
    /// <param name="connection">SQLite connection.</param>
    /// <param name="originId">Origin ID to look up.</param>
    /// <returns>Sync client if found, null if not found, or database error.</returns>
    public static SyncClientResult GetByOrigin(SqliteConnection connection, string originId)
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

            var client = ReadClient(reader: reader);

            return new SyncClientOk(client);
        }
        catch (SqliteException ex)
        {
            return new SyncClientError(
                new SyncErrorDatabase($"Failed to get sync client: {ex.Message}")
            );
        }
    }

    /// <summary>
    /// Upserts a sync client record.
    /// Inserts if not exists, updates if exists.
    /// </summary>
    /// <param name="connection">SQLite connection.</param>
    /// <param name="client">Client to upsert.</param>
    /// <returns>Success or database error.</returns>
    public static BoolSyncResult Upsert(SqliteConnection connection, SyncClient client) =>
        Upsert(
            connection: connection,
            client: client,
            failurePrefix: "Failed to upsert sync client"
        );

    internal static BoolSyncResult Upsert(
        SqliteConnection connection,
        SyncClient client,
        string failurePrefix
    )
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
        catch (SqliteException ex)
        {
            return new BoolSyncError(new SyncErrorDatabase($"{failurePrefix}: {ex.Message}"));
        }
    }

    /// <summary>
    /// Deletes a sync client by origin ID.
    /// </summary>
    /// <param name="connection">SQLite connection.</param>
    /// <param name="originId">Origin ID to delete.</param>
    /// <returns>Success or database error.</returns>
    public static BoolSyncResult Delete(SqliteConnection connection, string originId)
    {
        try
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "DELETE FROM _sync_clients WHERE origin_id = @originId";
            cmd.Parameters.AddWithValue("@originId", originId);
            cmd.ExecuteNonQuery();
            return new BoolSyncOk(true);
        }
        catch (SqliteException ex)
        {
            return new BoolSyncError(
                new SyncErrorDatabase($"Failed to delete sync client: {ex.Message}")
            );
        }
    }

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
        DeleteMultiple(
            connection: connection,
            originIds: originIds,
            failurePrefix: "Failed to delete sync clients"
        );

    internal static IntSyncResult DeleteMultiple(
        SqliteConnection connection,
        IEnumerable<string> originIds,
        string failurePrefix
    )
    {
        try
        {
            var deleted = 0;
            foreach (var originId in originIds)
            {
                using var cmd = connection.CreateCommand();
                cmd.CommandText = "DELETE FROM _sync_clients WHERE origin_id = @originId";
                cmd.Parameters.AddWithValue("@originId", originId);
                deleted += cmd.ExecuteNonQuery();
            }

            return new IntSyncOk(deleted);
        }
        catch (SqliteException ex)
        {
            return new IntSyncError(new SyncErrorDatabase($"{failurePrefix}: {ex.Message}"));
        }
    }

    private static SyncClient ReadClient(SqliteDataReader reader) =>
        new(
            OriginId: reader.GetString(0),
            LastSyncVersion: reader.GetInt64(1),
            LastSyncTimestamp: reader.GetString(2),
            CreatedAt: reader.GetString(3)
        );
}
