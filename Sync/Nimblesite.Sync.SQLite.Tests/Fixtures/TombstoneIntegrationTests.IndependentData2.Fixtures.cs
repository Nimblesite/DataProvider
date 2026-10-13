// Implements [CI-DESLOP-INDEPENDENT-DATA].

namespace Nimblesite.Sync.SQLite.Tests;

public partial class TombstoneIntegrationTests
{
    private global::Nimblesite.Sync.Core.SyncClient Dataset_Spec13_3_UpdateClientSyncState_TracksVersion_1() =>
        new SyncClient(_clientOrigin, 50, "2024-12-01T00:00:00.000Z", "2024-01-01T00:00:00.000Z");

    private static global::Nimblesite.Sync.Core.SyncClient Dataset_Spec13_5_StaleClients_IdentifiedByInactivity_1() =>
        new SyncClient(
            "stale-client",
            100,
            "2025-01-01T00:00:00.000Z", // 100 days ago
            "2024-01-01T00:00:00.000Z"
        );

    private static global::Nimblesite.Sync.Core.SyncClient Dataset_Spec13_5_StaleClients_IdentifiedByInactivity_2() =>
        new SyncClient(
            "active-client",
            200,
            "2025-04-01T00:00:00.000Z", // 9 days ago
            "2024-01-01T00:00:00.000Z"
        );

    private static global::Nimblesite.Sync.Core.SyncClient Dataset_Spec13_7_TombstonesPreserved_UntilAllClientsSynced_1() =>
        new SyncClient("fast-client", 100, "2025-01-01T00:00:00.000Z", "2025-01-01T00:00:00.000Z");

    private static global::Nimblesite.Sync.Core.SyncClient Dataset_Spec13_7_TombstonesPreserved_UntilAllClientsSynced_2() =>
        new SyncClient("slow-client", 20, "2025-01-01T00:00:00.000Z", "2025-01-01T00:00:00.000Z");
}
