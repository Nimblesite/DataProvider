// Implements [CI-TEST-INDEPENDENT-SCHEMA-DATA].
// Pure immutable fixture definitions; assertions and database workflows remain analysed.

namespace Nimblesite.Sync.Tests;

public sealed partial class ConflictResolverTests
{
    private static SyncLogEntry CreateEntry(
        long version,
        string tableName,
        string pkValue,
        string origin,
        string timestamp
    ) =>
        new(
            version,
            tableName,
            pkValue,
            SyncOperation.Update,
            "{\"Id\":\"" + pkValue + "\"}",
            origin,
            timestamp
        );
}
