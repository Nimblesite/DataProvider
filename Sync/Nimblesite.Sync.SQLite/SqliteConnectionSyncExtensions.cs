using Microsoft.Data.Sqlite;

namespace Nimblesite.Sync.SQLite;

/// <summary>
/// Extension methods for SQLite sync operations.
/// FP-style static methods on SqliteConnection.
/// </summary>
public static class SqliteConnectionSyncExtensions
{
    // === Subscription Operations (Spec Section 10) ===

    /// <summary>
    /// Gets all subscriptions from _sync_subscriptions.
    /// </summary>
    public static SubscriptionListResult GetAllSubscriptions(this SqliteConnection connection) =>
        SubscriptionRepository.GetAll(connection: connection);

    /// <summary>
    /// Gets subscriptions for a specific table.
    /// </summary>
    public static SubscriptionListResult GetSubscriptionsByTable(
        this SqliteConnection connection,
        string tableName
    )
    {
        try
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = """
                SELECT subscription_id, origin_id, subscription_type, table_name, filter, created_at, expires_at
                FROM _sync_subscriptions
                WHERE table_name = @tableName
                """;
            cmd.Parameters.AddWithValue("@tableName", tableName);

            var subscriptions = new List<SyncSubscription>();
            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                subscriptions.Add(SubscriptionRepository.ReadSubscription(reader: reader));
            }

            return new SubscriptionListOk(subscriptions);
        }
        catch (SqliteException ex)
        {
            return new SubscriptionListError(
                new SyncErrorDatabase($"Failed to get subscriptions: {ex.Message}")
            );
        }
    }

    /// <summary>
    /// Inserts a subscription into _sync_subscriptions.
    /// </summary>
    public static BoolSyncResult InsertSubscription(
        this SqliteConnection connection,
        SyncSubscription subscription
    ) => SubscriptionRepository.Insert(connection: connection, subscription: subscription);

    /// <summary>
    /// Deletes a subscription by ID.
    /// </summary>
    public static BoolSyncResult DeleteSubscription(
        this SqliteConnection connection,
        string subscriptionId
    ) => SubscriptionRepository.Delete(connection: connection, subscriptionId: subscriptionId);

    /// <summary>
    /// Deletes all subscriptions for an origin.
    /// </summary>
    public static IntSyncResult DeleteSubscriptionsByOrigin(
        this SqliteConnection connection,
        string originId
    ) => SubscriptionRepository.DeleteByOrigin(connection: connection, originId: originId);

    /// <summary>
    /// Deletes expired subscriptions.
    /// </summary>
    public static IntSyncResult DeleteExpiredSubscriptions(
        this SqliteConnection connection,
        string currentTimestamp
    ) =>
        SubscriptionRepository.DeleteExpired(
            connection: connection,
            currentTimestamp: currentTimestamp
        );

    // === Tombstone Operations (Spec Section 13) ===

    /// <summary>
    /// Gets the oldest version in the sync log.
    /// Used to detect if a client has fallen behind.
    /// </summary>
    public static LongSyncResult GetOldestSyncLogVersion(this SqliteConnection connection)
    {
        try
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT MIN(version) FROM _sync_log";

            var result = cmd.ExecuteScalar();
            var version = result is long v ? v : 0;

            return new LongSyncOk(version);
        }
        catch (SqliteException ex)
        {
            return new LongSyncError(
                new SyncErrorDatabase($"Failed to get oldest version: {ex.Message}")
            );
        }
    }

    /// <summary>
    /// Purges tombstones (delete entries) below a given version.
    /// </summary>
    public static IntSyncResult PurgeTombstones(this SqliteConnection connection, long belowVersion)
    {
        try
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = """
                DELETE FROM _sync_log
                WHERE operation = 'delete'
                  AND version < @belowVersion
                """;
            cmd.Parameters.AddWithValue("@belowVersion", belowVersion);

            var deleted = cmd.ExecuteNonQuery();
            return new IntSyncOk(deleted);
        }
        catch (SqliteException ex)
        {
            return new IntSyncError(
                new SyncErrorDatabase($"Failed to purge tombstones: {ex.Message}")
            );
        }
    }

    /// <summary>
    /// Purges all sync log entries below a given version.
    /// Use with caution - only after all clients have synced past this version.
    /// </summary>
    public static IntSyncResult PurgeSyncLog(this SqliteConnection connection, long belowVersion)
    {
        try
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "DELETE FROM _sync_log WHERE version < @belowVersion";
            cmd.Parameters.AddWithValue("@belowVersion", belowVersion);

            var deleted = cmd.ExecuteNonQuery();
            return new IntSyncOk(deleted);
        }
        catch (SqliteException ex)
        {
            return new IntSyncError(
                new SyncErrorDatabase($"Failed to purge sync log: {ex.Message}")
            );
        }
    }

    // === Client Tracking Operations ===

    /// <summary>
    /// Gets all tracked sync clients.
    /// </summary>
    public static SyncClientListResult GetAllSyncClients(this SqliteConnection connection)
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
                clients.Add(
                    new SyncClient(
                        OriginId: reader.GetString(0),
                        LastSyncVersion: reader.GetInt64(1),
                        LastSyncTimestamp: reader.GetString(2),
                        CreatedAt: reader.GetString(3)
                    )
                );
            }

            return new SyncClientListOk(clients);
        }
        catch (SqliteException ex)
        {
            return new SyncClientListError(
                new SyncErrorDatabase($"Failed to get clients: {ex.Message}")
            );
        }
    }

    /// <summary>
    /// Upserts a sync client record.
    /// </summary>
    public static BoolSyncResult UpsertSyncClient(
        this SqliteConnection connection,
        SyncClient client
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
            return new BoolSyncError(
                new SyncErrorDatabase($"Failed to upsert client: {ex.Message}")
            );
        }
    }

    /// <summary>
    /// Deletes stale sync clients.
    /// </summary>
    public static IntSyncResult DeleteStaleSyncClients(
        this SqliteConnection connection,
        IEnumerable<string> originIds
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
            return new IntSyncError(
                new SyncErrorDatabase($"Failed to delete stale clients: {ex.Message}")
            );
        }
    }
}
