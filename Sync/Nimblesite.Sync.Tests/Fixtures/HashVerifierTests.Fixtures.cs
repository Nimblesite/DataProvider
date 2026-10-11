// Implements [CI-DESLOP-INDEPENDENT-DATA].


namespace Nimblesite.Sync.Tests;

public partial class HashVerifierTests
{
    private static global::Nimblesite.Sync.Core.SyncLogEntry Fixture_ComputeBatchHash_SameEntries_SameHash_1() =>
        new SyncLogEntry(
                1,
                "Person",
                "{\"Id\":\"1\"}",
                SyncOperation.Insert,
                "{\"Id\":\"1\",\"Name\":\"Alice\"}",
                "origin-1",
                "2025-01-01T00:00:00.000Z"
            );

    private static global::Nimblesite.Sync.Core.SyncLogEntry Fixture_ComputeBatchHash_SameEntries_SameHash_2() =>
        new SyncLogEntry(
                2,
                "Person",
                "{\"Id\":\"2\"}",
                SyncOperation.Insert,
                "{\"Id\":\"2\",\"Name\":\"Bob\"}",
                "origin-1",
                "2025-01-01T00:00:01.000Z"
            );

    private static global::Nimblesite.Sync.Core.SyncLogEntry Fixture_ComputeBatchHash_DifferentEntries_DifferentHash_1() =>
        new SyncLogEntry(
                1,
                "Person",
                "{\"Id\":\"1\"}",
                SyncOperation.Insert,
                "{\"Id\":\"1\",\"Name\":\"Alice\"}",
                "origin-1",
                "2025-01-01T00:00:00.000Z"
            );

    private static global::Nimblesite.Sync.Core.SyncLogEntry Fixture_ComputeBatchHash_DifferentEntries_DifferentHash_2() =>
        new SyncLogEntry(
                1,
                "Person",
                "{\"Id\":\"1\"}",
                SyncOperation.Insert,
                "{\"Id\":\"1\",\"Name\":\"Bob\"}",
                "origin-1",
                "2025-01-01T00:00:00.000Z"
            );

    private static global::Nimblesite.Sync.Core.SyncLogEntry Fixture_ComputeBatchHash_DeleteWithNullPayload_HandlesCorrectly_1() =>
        new SyncLogEntry(
                1,
                "Person",
                "{\"Id\":\"1\"}",
                SyncOperation.Delete,
                null,
                "origin-1",
                "2025-01-01T00:00:00.000Z"
            );
}
