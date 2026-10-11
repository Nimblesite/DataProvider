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
            global::Nimblesite.Sync.SQLite.MappingStateRepository.SqlSpecification8
        );

    /// <summary>
    /// Gets a subscription by ID.
    /// </summary>
    /// <param name="connection">SQLite connection.</param>
    /// <param name="subscriptionId">Subscription ID to look up.</param>
    /// <returns>Subscription if found, null if not found, or database error.</returns>
    public static SubscriptionResult GetById(SqliteConnection connection, string subscriptionId)
    {
        return SyncCommandExecution.Read<SqliteException, SubscriptionResult>(
            connection: connection,
            setSql: command =>
                command.CommandText = global::Nimblesite
                    .Sync
                    .SQLite
                    .MappingStateRepository
                    .SqlSpecification9,
            parameters: () => [("@subscriptionId", subscriptionId)],
            read: command =>
            {
                using var reader = command.ExecuteReader();

                if (!reader.Read())
                {
                    return new SubscriptionOk(null);
                }

                return new SubscriptionOk(ReadSubscription(reader));
            },
            failure: ex =>
            {
                return new SubscriptionError(
                    new SyncErrorDatabase($"Failed to get subscription: {ex.Message}")
                );
            }
        );
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
            global::Nimblesite.Sync.SQLite.MappingStateRepository.SqlSpecification10,
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
            global::Nimblesite.Sync.SQLite.MappingStateRepository.SqlSpecification11,
            cmd => cmd.Parameters.AddWithValue("@originId", originId)
        );

    /// <summary>
    /// Inserts a subscription into _sync_subscriptions.
    /// </summary>
    /// <param name="connection">SQLite connection.</param>
    /// <param name="subscription">Subscription to insert.</param>
    /// <returns>Success or database error.</returns>
    public static BoolSyncResult Insert(
        SqliteConnection connection,
        SyncSubscription subscription
    ) =>
        SyncCommandExecution.Write<SqliteException>(
            connection: connection,
            setSql: command =>
                command.CommandText = global::Nimblesite
                    .Sync
                    .SQLite
                    .MappingStateRepository
                    .SqlSpecification12,
            parameters: () =>
                [
                    ("@subscriptionId", subscription.SubscriptionId),
                    ("@originId", subscription.OriginId),
                    ("@subscriptionType", subscription.Type.ToString().ToLowerInvariant()),
                    ("@tableName", subscription.TableName),
                    ("@filter", subscription.Filter ?? (object)DBNull.Value),
                    ("@createdAt", subscription.CreatedAt),
                    ("@expiresAt", subscription.ExpiresAt ?? (object)DBNull.Value),
                ],
            failurePrefix: "Failed to insert subscription"
        );

    /// <summary>
    /// Deletes a subscription by ID.
    /// </summary>
    /// <param name="connection">SQLite connection.</param>
    /// <param name="subscriptionId">Subscription ID to delete.</param>
    /// <returns>Success or database error.</returns>
    public static BoolSyncResult Delete(SqliteConnection connection, string subscriptionId) =>
        SyncCommandExecution.Write<SqliteException>(
            connection: connection,
            setSql: command =>
                command.CommandText =
                    "DELETE FROM _sync_subscriptions WHERE subscription_id = @subscriptionId",
            parameters: () => [("@subscriptionId", subscriptionId)],
            failurePrefix: "Failed to delete subscription"
        );

    /// <summary>
    /// Deletes all subscriptions for an origin.
    /// </summary>
    /// <param name="connection">SQLite connection.</param>
    /// <param name="originId">Origin ID to delete subscriptions for.</param>
    /// <returns>Number of deleted subscriptions or database error.</returns>
    public static IntSyncResult DeleteByOrigin(SqliteConnection connection, string originId) =>
        SyncCommandExecution.WriteCount<SqliteException>(
            connection: connection,
            setSql: command =>
                command.CommandText = "DELETE FROM _sync_subscriptions WHERE origin_id = @originId",
            parameters: () => [("@originId", originId)],
            failurePrefix: "Failed to delete subscriptions"
        );

    /// <summary>
    /// Deletes expired subscriptions.
    /// </summary>
    /// <param name="connection">SQLite connection.</param>
    /// <param name="currentTimestamp">Current timestamp to compare against.</param>
    /// <returns>Number of deleted subscriptions or database error.</returns>
    public static IntSyncResult DeleteExpired(
        SqliteConnection connection,
        string currentTimestamp
    ) =>
        SyncCommandExecution.WriteCount<SqliteException>(
            connection: connection,
            setSql: command =>
                command.CommandText = global::Nimblesite
                    .Sync
                    .SQLite
                    .MappingStateRepository
                    .SqlSpecification13,
            parameters: () => [("@currentTimestamp", currentTimestamp)],
            failurePrefix: "Failed to delete expired subscriptions"
        );

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

            return new SubscriptionListOk(
                SyncCommandExecution.ReadRows<SyncSubscription>(
                    command: cmd,
                    read: row => ReadSubscription(row)
                )
            );
        }
        catch (SqliteException ex)
        {
            return new SubscriptionListError(
                new SyncErrorDatabase($"Failed to get subscriptions: {ex.Message}")
            );
        }
    }

    internal static SyncSubscription ReadSubscription(
        global::System.Data.Common.DbDataReader reader
    ) =>
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
