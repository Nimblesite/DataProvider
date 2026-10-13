// Implements [CI-DESLOP-INDEPENDENT-DATA].

namespace Nimblesite.Sync.Tests;

public partial class SyncStateTests
{
    private static global::Nimblesite.Sync.Core.SyncClient Dataset_SyncClient_CreatesCorrectRecord_1() =>
        new SyncClient("client-001", 100, "2024-01-01T00:00:00Z", "2024-01-01T00:00:00Z");

    private static global::Nimblesite.Sync.Core.SyncSubscription Dataset_SyncSubscription_CreatesCorrectRecord_1() =>
        new SyncSubscription(
            "sub-1",
            "origin-1",
            SubscriptionType.Table,
            "Person",
            null,
            "2024-01-01T00:00:00Z",
            "2024-12-31T00:00:00Z"
        );
}
