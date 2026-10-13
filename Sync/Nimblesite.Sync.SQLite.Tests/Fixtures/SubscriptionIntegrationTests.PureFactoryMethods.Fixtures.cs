// Implements [CI-TEST-INDEPENDENT-SCHEMA-DATA].
// Pure immutable fixture definitions; assertions and database workflows remain analysed.

namespace Nimblesite.Sync.SQLite.Tests;

public sealed partial class SubscriptionIntegrationTests
{
    private static SyncLogEntry CreateChange(
        string tableName,
        string pkValue,
        SyncOperation operation,
        long version = 1
    ) =>
        new(
            version,
            tableName,
            pkValue,
            operation,
            operation == SyncOperation.Delete ? null : "{\"Id\":\"test\"}",
            "server-origin",
            Timestamp
        );
}
