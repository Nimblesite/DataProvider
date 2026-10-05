using Microsoft.Data.Sqlite;

namespace Nimblesite.Sync.SQLite;

// Implements [SYNC-SCALAR-READ-SHARED].

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
    public static LongSyncResult GetOldestSyncLogVersion(this SqliteConnection connection) =>
        SqliteCommandExecution.ReadInt64OrZero(
            connection: connection,
            setSql: command => command.CommandText = "SELECT MIN(version) FROM _sync_log",
            failurePrefix: "Failed to get oldest version"
        );

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
    // Implements [SYNC-CLIENT-API-SHARED].
    public static SyncClientListResult GetAllSyncClients(this SqliteConnection connection) =>
        SyncClientRepository.GetAll(connection: connection, failurePrefix: "Failed to get clients");

    /// <summary>Upserts a sync client record.</summary>
    public static BoolSyncResult UpsertSyncClient(
        this SqliteConnection connection,
        SyncClient client
    ) =>
        SyncClientRepository.Upsert(
            connection: connection,
            client: client,
            failurePrefix: "Failed to upsert client"
        );

    /// <summary>Deletes stale sync clients.</summary>
    public static IntSyncResult DeleteStaleSyncClients(
        this SqliteConnection connection,
        IEnumerable<string> originIds
    ) =>
        SyncClientRepository.DeleteMultiple(
            connection: connection,
            originIds: originIds,
            failurePrefix: "Failed to delete stale clients"
        );
}
