using System.Diagnostics.CodeAnalysis;
using Microsoft.Data.Sqlite;

namespace Nimblesite.Sync.SQLite;

/// <summary>
/// Repository for managing sync subscriptions in SQLite.
/// Implements spec Section 10 (Real-Time Subscriptions).
/// </summary>
public static class SubscriptionRepository
{
    /// <summary>
    /// Gets all subscriptions from _sync_subscriptions table.
    /// </summary>
    /// <param name="connection">SQLite connection.</param>
    /// <returns>List of subscriptions or database error.</returns>
    public static SubscriptionListResult GetAll(SqliteConnection connection) =>
        QueryList(
            connection,
            """
            SELECT subscription_id, origin_id, subscription_type, table_name, filter, created_at, expires_at
            FROM _sync_subscriptions
            ORDER BY created_at ASC
            """
        );

    /// <summary>
    /// Gets a subscription by ID.
    /// </summary>
    /// <param name="connection">SQLite connection.</param>
    /// <param name="subscriptionId">Subscription ID to look up.</param>
    /// <returns>Subscription if found, null if not found, or database error.</returns>
    public static SubscriptionResult GetById(SqliteConnection connection, string subscriptionId)
    {
        try
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = """
                SELECT subscription_id, origin_id, subscription_type, table_name, filter, created_at, expires_at
                FROM _sync_subscriptions
                WHERE subscription_id = @subscriptionId
                """;
            cmd.Parameters.AddWithValue("@subscriptionId", subscriptionId);

            using var reader = cmd.ExecuteReader();

            if (!reader.Read())
            {
                return new SubscriptionOk(null);
            }

            return new SubscriptionOk(ReadSubscription(reader));
        }
        catch (SqliteException ex)
        {
            return new SubscriptionError(
                new SyncErrorDatabase($"Failed to get subscription: {ex.Message}")
            );
        }
    }

    /// <summary>
    /// Gets subscriptions for a specific table.
    /// </summary>
    /// <param name="connection">SQLite connection.</param>
    /// <param name="tableName">Table name to filter by.</param>
    /// <returns>List of subscriptions for the table or database error.</returns>
    public static SubscriptionListResult GetByTable(
        SqliteConnection connection,
        string tableName
    ) =>
        QueryList(
            connection,
            """
            SELECT subscription_id, origin_id, subscription_type, table_name, filter, created_at, expires_at
            FROM _sync_subscriptions
            WHERE table_name = @tableName
            ORDER BY created_at ASC
            """,
            cmd => cmd.Parameters.AddWithValue("@tableName", tableName)
        );

    /// <summary>
    /// Gets subscriptions for a specific origin.
    /// </summary>
    /// <param name="connection">SQLite connection.</param>
    /// <param name="originId">Origin ID to filter by.</param>
    /// <returns>List of subscriptions for the origin or database error.</returns>
    public static SubscriptionListResult GetByOrigin(
        SqliteConnection connection,
        string originId
    ) =>
        QueryList(
            connection,
            """
            SELECT subscription_id, origin_id, subscription_type, table_name, filter, created_at, expires_at
            FROM _sync_subscriptions
            WHERE origin_id = @originId
            ORDER BY created_at ASC
            """,
            cmd => cmd.Parameters.AddWithValue("@originId", originId)
        );

    /// <summary>
    /// Inserts a subscription into _sync_subscriptions.
    /// </summary>
    /// <param name="connection">SQLite connection.</param>
    /// <param name="subscription">Subscription to insert.</param>
    /// <returns>Success or database error.</returns>
    public static BoolSyncResult Insert(SqliteConnection connection, SyncSubscription subscription)
    {
        try
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = """
                INSERT INTO _sync_subscriptions
                    (subscription_id, origin_id, subscription_type, table_name, filter, created_at, expires_at)
                VALUES
                    (@subscriptionId, @originId, @subscriptionType, @tableName, @filter, @createdAt, @expiresAt)
                """;
            cmd.Parameters.AddWithValue("@subscriptionId", subscription.SubscriptionId);
            cmd.Parameters.AddWithValue("@originId", subscription.OriginId);
            cmd.Parameters.AddWithValue(
                "@subscriptionType",
                subscription.Type.ToString().ToLowerInvariant()
            );
            cmd.Parameters.AddWithValue("@tableName", subscription.TableName);
            cmd.Parameters.AddWithValue("@filter", subscription.Filter ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@createdAt", subscription.CreatedAt);
            cmd.Parameters.AddWithValue(
                "@expiresAt",
                subscription.ExpiresAt ?? (object)DBNull.Value
            );

            cmd.ExecuteNonQuery();
            return new BoolSyncOk(true);
        }
        catch (SqliteException ex)
        {
            return new BoolSyncError(
                new SyncErrorDatabase($"Failed to insert subscription: {ex.Message}")
            );
        }
    }

    /// <summary>
    /// Deletes a subscription by ID.
    /// </summary>
    /// <param name="connection">SQLite connection.</param>
    /// <param name="subscriptionId">Subscription ID to delete.</param>
    /// <returns>Success or database error.</returns>
    public static BoolSyncResult Delete(SqliteConnection connection, string subscriptionId)
    {
        try
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText =
                "DELETE FROM _sync_subscriptions WHERE subscription_id = @subscriptionId";
            cmd.Parameters.AddWithValue("@subscriptionId", subscriptionId);
            cmd.ExecuteNonQuery();
            return new BoolSyncOk(true);
        }
        catch (SqliteException ex)
        {
            return new BoolSyncError(
                new SyncErrorDatabase($"Failed to delete subscription: {ex.Message}")
            );
        }
    }

    /// <summary>
    /// Deletes all subscriptions for an origin.
    /// </summary>
    /// <param name="connection">SQLite connection.</param>
    /// <param name="originId">Origin ID to delete subscriptions for.</param>
    /// <returns>Number of deleted subscriptions or database error.</returns>
    public static IntSyncResult DeleteByOrigin(SqliteConnection connection, string originId)
    {
        try
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "DELETE FROM _sync_subscriptions WHERE origin_id = @originId";
            cmd.Parameters.AddWithValue("@originId", originId);

            var deleted = cmd.ExecuteNonQuery();
            return new IntSyncOk(deleted);
        }
        catch (SqliteException ex)
        {
            return new IntSyncError(
                new SyncErrorDatabase($"Failed to delete subscriptions: {ex.Message}")
            );
        }
    }

    /// <summary>
    /// Deletes expired subscriptions.
    /// </summary>
    /// <param name="connection">SQLite connection.</param>
    /// <param name="currentTimestamp">Current timestamp to compare against.</param>
    /// <returns>Number of deleted subscriptions or database error.</returns>
    public static IntSyncResult DeleteExpired(SqliteConnection connection, string currentTimestamp)
    {
        try
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = """
                DELETE FROM _sync_subscriptions
                WHERE expires_at IS NOT NULL AND expires_at < @currentTimestamp
                """;
            cmd.Parameters.AddWithValue("@currentTimestamp", currentTimestamp);

            var deleted = cmd.ExecuteNonQuery();
            return new IntSyncOk(deleted);
        }
        catch (SqliteException ex)
        {
            return new IntSyncError(
                new SyncErrorDatabase($"Failed to delete expired subscriptions: {ex.Message}")
            );
        }
    }

    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "SQL comes from internal constant literals, not user input"
    )]
    private static SubscriptionListResult QueryList(
        SqliteConnection connection,
        string sql,
        Action<SqliteCommand>? bindParameters = null
    )
    {
        try
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = sql;
            bindParameters?.Invoke(cmd);

            var subscriptions = new List<SyncSubscription>();
            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                subscriptions.Add(ReadSubscription(reader));
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

    internal static SyncSubscription ReadSubscription(SqliteDataReader reader) =>
        new(
            SubscriptionId: reader.GetString(0),
            OriginId: reader.GetString(1),
            Type: ParseSubscriptionType(reader.GetString(2)),
            TableName: reader.GetString(3),
            Filter: reader.IsDBNull(4) ? null : reader.GetString(4),
            CreatedAt: reader.GetString(5),
            ExpiresAt: reader.IsDBNull(6) ? null : reader.GetString(6)
        );

    internal static SubscriptionType ParseSubscriptionType(string type) =>
        type.ToLowerInvariant() switch
        {
            "record" => SubscriptionType.Record,
            "table" => SubscriptionType.Table,
            "query" => SubscriptionType.Query,
            _ => SubscriptionType.Table,
        };
}
