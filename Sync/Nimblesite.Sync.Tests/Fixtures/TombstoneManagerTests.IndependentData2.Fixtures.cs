// Implements [CI-DESLOP-INDEPENDENT-DATA].

namespace Nimblesite.Sync.Tests;

public partial class TombstoneManagerTests
{
    private static global::Nimblesite.Sync.Core.SyncClient Dataset_UpdateClientSyncState_ExistingClient_UpdatesVersionAndTimestamp_1() =>
        new SyncClient("origin-1", 50, "2024-12-01T00:00:00.000Z", "2024-01-01T00:00:00.000Z");
}
