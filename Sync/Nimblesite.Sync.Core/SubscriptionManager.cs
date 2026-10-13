namespace Nimblesite.Sync.Core;

/// <summary>
/// Type of subscription for real-time notifications.
/// </summary>
public enum SubscriptionType
{
    /// <summary>
    /// Subscribe to specific primary key(s).
    /// </summary>
    Record,

    /// <summary>
    /// Subscribe to all changes in a table.
    /// </summary>
    Table,

    /// <summary>
    /// Subscribe to records matching query criteria.
    /// </summary>
    Query,
}

/// <summary>
/// Represents a real-time subscription. Maps to spec Section 10.6.
/// </summary>
/// <param name="SubscriptionId">Unique identifier for this subscription.</param>
/// <param name="OriginId">Origin ID of the subscribing client.</param>
/// <param name="Type">Type of subscription (record, table, query).</param>
/// <param name="TableName">Table being subscribed to.</param>
/// <param name="Filter">JSON filter: pk_values for record, query criteria for query. Null for table.</param>
/// <param name="CreatedAt">ISO 8601 UTC timestamp when subscription was created.</param>
/// <param name="ExpiresAt">Optional expiration timestamp. Null for no expiry.</param>
public sealed record SyncSubscription(
    string SubscriptionId,
    string OriginId,
    SubscriptionType Type,
    string TableName,
    string? Filter,
    string CreatedAt,
    string? ExpiresAt
);

/// <summary>
/// Represents a change notification to be sent to subscribers.
/// </summary>
/// <param name="SubscriptionId">The subscription that triggered this notification.</param>
/// <param name="Change">The change that occurred.</param>
public sealed record ChangeNotification(string SubscriptionId, SyncLogEntry Change);

/// <summary>
/// Manages real-time subscriptions for sync notifications.
/// Implements spec Section 10 (Real-Time Subscriptions).
/// </summary>
internal static class SubscriptionManager
{
    /// <summary>
    /// Creates a new record-level subscription.
    /// </summary>
    /// <param name="subscriptionId">Unique subscription ID.</param>
    /// <param name="originId">Subscribing client's origin ID.</param>
    /// <param name="tableName">Table to subscribe to.</param>
    /// <param name="pkValues">Primary key values to watch (JSON array).</param>
    /// <param name="timestamp">Current UTC timestamp.</param>
    /// <param name="expiresAt">Optional expiration timestamp.</param>
    /// <returns>New subscription record.</returns>
    public static SyncSubscription CreateRecordSubscription(
        string subscriptionId,
        string originId,
        string tableName,
        string pkValues,
        string timestamp,
        string? expiresAt = null
    ) =>
        CreateSubscription(
            subscriptionId: subscriptionId,
            originId: originId,
            type: SubscriptionType.Record,
            tableName: tableName,
            filter: pkValues,
            timestamp: timestamp,
            expiresAt: expiresAt
        );

    /// <summary>
    /// Creates a new table-level subscription.
    /// </summary>
    /// <param name="subscriptionId">Unique subscription ID.</param>
    /// <param name="originId">Subscribing client's origin ID.</param>
    /// <param name="tableName">Table to subscribe to.</param>
    /// <param name="timestamp">Current UTC timestamp.</param>
    /// <param name="expiresAt">Optional expiration timestamp.</param>
    /// <returns>New subscription record.</returns>
    public static SyncSubscription CreateTableSubscription(
        string subscriptionId,
        string originId,
        string tableName,
        string timestamp,
        string? expiresAt = null
    ) =>
        CreateSubscription(
            subscriptionId: subscriptionId,
            originId: originId,
            type: SubscriptionType.Table,
            tableName: tableName,
            filter: null,
            timestamp: timestamp,
            expiresAt: expiresAt
        );

    /// <summary>
    /// Creates a new query-level subscription.
    /// </summary>
    /// <param name="subscriptionId">Unique subscription ID.</param>
    /// <param name="originId">Subscribing client's origin ID.</param>
    /// <param name="tableName">Table to subscribe to.</param>
    /// <param name="queryCriteria">JSON query criteria for filtering.</param>
    /// <param name="timestamp">Current UTC timestamp.</param>
    /// <param name="expiresAt">Optional expiration timestamp.</param>
    /// <returns>New subscription record.</returns>
    public static SyncSubscription CreateQuerySubscription(
        string subscriptionId,
        string originId,
        string tableName,
        string queryCriteria,
        string timestamp,
        string? expiresAt = null
    ) =>
        CreateSubscription(
            subscriptionId: subscriptionId,
            originId: originId,
            type: SubscriptionType.Query,
            tableName: tableName,
            filter: queryCriteria,
            timestamp: timestamp,
            expiresAt: expiresAt
        );

    private static SyncSubscription CreateSubscription(
        string subscriptionId,
        string originId,
        SubscriptionType type,
        string tableName,
        string? filter,
        string timestamp,
        string? expiresAt
    ) => new(subscriptionId, originId, type, tableName, filter, timestamp, expiresAt);

    /// <summary>
    /// Checks if a subscription matches a change.
    /// </summary>
    /// <param name="subscription">The subscription to check.</param>
    /// <param name="change">The change to match against.</param>
    /// <returns>True if the subscription should receive this change.</returns>
    public static bool MatchesChange(SyncSubscription subscription, SyncLogEntry change)
    {
        if (subscription.TableName != change.TableName)
        {
            return false;
        }

        return subscription.Type switch
        {
            SubscriptionType.Table => true,
            SubscriptionType.Record => MatchesRecordFilter(subscription.Filter, change.PkValue),
            SubscriptionType.Query => true, // Query matching requires application-level logic
            _ => false,
        };
    }

    /// <summary>
    /// Checks if a subscription has expired.
    /// </summary>
    /// <param name="subscription">The subscription to check.</param>
    /// <param name="currentTimestamp">Current UTC timestamp.</param>
    /// <returns>True if the subscription has expired.</returns>
    public static bool IsExpired(SyncSubscription subscription, string currentTimestamp) =>
        subscription.ExpiresAt is not null
        && string.Compare(currentTimestamp, subscription.ExpiresAt, StringComparison.Ordinal) > 0;

    /// <summary>
    /// Finds all subscriptions that match a change.
    /// </summary>
    /// <param name="subscriptions">All active subscriptions.</param>
    /// <param name="change">The change to find matching subscriptions for.</param>
    /// <returns>Matching subscriptions.</returns>
    public static IReadOnlyList<SyncSubscription> FindMatchingSubscriptions(
        IEnumerable<SyncSubscription> subscriptions,
        SyncLogEntry change
    ) => [.. subscriptions.Where(s => MatchesChange(s, change))];

    /// <summary>
    /// Creates notifications for all matching subscriptions.
    /// </summary>
    /// <param name="subscriptions">All active subscriptions.</param>
    /// <param name="change">The change to notify about.</param>
    /// <returns>Notifications to send.</returns>
    public static IReadOnlyList<ChangeNotification> CreateNotifications(
        IEnumerable<SyncSubscription> subscriptions,
        SyncLogEntry change
    ) =>
        [
            .. FindMatchingSubscriptions(subscriptions, change)
                .Select(s => new ChangeNotification(s.SubscriptionId, change)),
        ];

    /// <summary>
    /// Filters out expired subscriptions.
    /// </summary>
    /// <param name="subscriptions">Subscriptions to filter.</param>
    /// <param name="currentTimestamp">Current UTC timestamp.</param>
    /// <returns>Active (non-expired) subscriptions.</returns>
    public static IReadOnlyList<SyncSubscription> FilterExpired(
        IEnumerable<SyncSubscription> subscriptions,
        string currentTimestamp
    ) => [.. subscriptions.Where(s => !IsExpired(s, currentTimestamp))];

    private static bool MatchesRecordFilter(string? filter, string pkValue)
    {
        if (string.IsNullOrEmpty(filter))
        {
            return false;
        }

        // Extract actual PK value from JSON pkValue
        // pkValue format: {"Id":"p1"} or {"Id":"uuid-here"}
        // filter format: ["p1", "p2"] (JSON array of raw PK values)
        var actualPkValue = ExtractPkValue(pkValue);
        if (actualPkValue is null)
        {
            // Fallback to simple containment if JSON parsing fails
            return filter.Contains(pkValue, StringComparison.Ordinal);
        }

        // Check if filter contains this PK value
        // Filter is typically a JSON array like ["p1", "p2"]
        return filter.Contains($"\"{actualPkValue}\"", StringComparison.Ordinal);
    }

    private static string? ExtractPkValue(string pkValue)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(pkValue);
            var root = doc.RootElement;

            if (root.ValueKind != System.Text.Json.JsonValueKind.Object)
            {
                return null;
            }

            // Get the first property value (single-column PK)
            foreach (var prop in root.EnumerateObject())
            {
                return prop.Value.GetString();
            }

            return null;
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }
}
