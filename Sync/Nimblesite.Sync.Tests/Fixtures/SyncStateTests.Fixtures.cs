// Implements [CI-DESLOP-INDEPENDENT-DATA].


namespace Nimblesite.Sync.Tests;

public partial class SyncStateTests
{
    private static global::Nimblesite.Sync.Core.SyncLogEntry Fixture_SyncLogEntry_CreatesCorrectRecord_1() =>
        new SyncLogEntry(
            1,
            "Person",
            "{\"Id\":\"p1\"}",
            SyncOperation.Insert,
            "{\"Id\":\"p1\",\"Name\":\"Alice\"}",
            "origin-1",
            "2024-01-01T00:00:00Z"
        );

    private static global::Nimblesite.Sync.Core.SyncLogEntry Fixture_SyncLogEntry_DeleteHasNullPayload_1() =>
        new SyncLogEntry(
            1,
            "Person",
            "{\"Id\":\"p1\"}",
            SyncOperation.Delete,
            null,
            "origin-1",
            "2024-01-01T00:00:00Z"
        );

    private static global::Nimblesite.Sync.Core.SyncLogEntry Fixture_ChangeNotification_CreatesCorrectRecord_1() =>
        new SyncLogEntry(
            1,
            "Person",
            "{\"Id\":\"p1\"}",
            SyncOperation.Insert,
            "{}",
            "origin",
            "2024-01-01T00:00:00Z"
        );
}
