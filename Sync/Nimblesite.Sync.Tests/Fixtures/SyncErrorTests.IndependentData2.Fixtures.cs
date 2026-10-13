// Implements [CI-DESLOP-INDEPENDENT-DATA].

namespace Nimblesite.Sync.Tests;

public partial class SyncErrorTests
{
    private static global::Nimblesite.Sync.Core.SyncErrorForeignKeyViolation Dataset_SyncErrorForeignKeyViolation_CreatesCorrectRecord_1() =>
        new SyncErrorForeignKeyViolation("Person", "{\"Id\":\"p1\"}", "FK_Person_Department");

    private static global::Nimblesite.Sync.Core.SyncErrorDeferredChangeFailed Dataset_AllSyncErrors_DeriveFromSyncError_1() =>
        new SyncErrorDeferredChangeFailed(
            new SyncLogEntry(1, "T", "PK", SyncOperation.Insert, "{}", "O", "TS"),
            "Reason"
        );

    private static global::Nimblesite.Sync.Core.SyncErrorUnresolvedConflict Dataset_AllSyncErrors_DeriveFromSyncError_2() =>
        new SyncErrorUnresolvedConflict(
            new SyncLogEntry(1, "T", "PK", SyncOperation.Update, "{}", "O1", "TS1"),
            new SyncLogEntry(2, "T", "PK", SyncOperation.Update, "{}", "O2", "TS2")
        );
}
