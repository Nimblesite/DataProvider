// Implements [CI-DESLOP-INDEPENDENT-DATA].
namespace Nimblesite.Sync.SQLite.Tests;

public partial class SpecConformanceTests
{
    private static global::Nimblesite.Sync.Core.SyncLogEntry Fixture_Spec15_4_BatchHash_IsConsistent_1() =>
        new SyncLogEntry(
            1,
            "Person",
            "{\"Id\":\"1\"}",
            SyncOperation.Insert,
            "{\"Id\":\"1\",\"Name\":\"Alice\"}",
            "origin-1",
            "2025-01-01T00:00:00.000Z"
        );
}
