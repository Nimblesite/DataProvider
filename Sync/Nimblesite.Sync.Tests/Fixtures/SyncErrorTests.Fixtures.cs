// Implements [CI-DESLOP-INDEPENDENT-DATA].


namespace Nimblesite.Sync.Tests;

public partial class SyncErrorTests
{
    private static global::Nimblesite.Sync.Core.SyncLogEntry Fixture_SyncErrorDeferredChangeFailed_CreatesCorrectRecord_1() =>
        new SyncLogEntry(
            1,
            "Person",
            "{\"Id\":\"p1\"}",
            SyncOperation.Insert,
            "{\"Id\":\"p1\",\"Name\":\"Alice\"}",
            "origin-1",
            "2024-01-01T00:00:00Z"
        );

    private static global::Nimblesite.Sync.Core.SyncLogEntry Fixture_SyncErrorUnresolvedConflict_CreatesCorrectRecord_1() =>
        new SyncLogEntry(
            1,
            "Person",
            "{\"Id\":\"p1\"}",
            SyncOperation.Update,
            "{\"Id\":\"p1\",\"Name\":\"LocalName\"}",
            "local-origin",
            "2024-01-01T00:00:00Z"
        );

    private static global::Nimblesite.Sync.Core.SyncLogEntry Fixture_SyncErrorUnresolvedConflict_CreatesCorrectRecord_2() =>
        new SyncLogEntry(
            2,
            "Person",
            "{\"Id\":\"p1\"}",
            SyncOperation.Update,
            "{\"Id\":\"p1\",\"Name\":\"RemoteName\"}",
            "remote-origin",
            "2024-01-01T00:00:01Z"
        );
}
