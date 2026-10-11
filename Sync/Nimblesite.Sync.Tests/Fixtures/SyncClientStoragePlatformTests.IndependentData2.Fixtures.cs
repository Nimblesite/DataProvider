// Implements [CI-DESLOP-INDEPENDENT-DATA].

namespace Nimblesite.Sync.Tests;

public partial record SyncClientStoragePlatformTests
{
    private static global::Nimblesite.Sync.Core.SyncClient Dataset_AssertRoundTrips_1() =>
        new SyncClient(
            OriginId: "O'Brien 雪",
            LastSyncVersion: 2,
            LastSyncTimestamp: "2024-01-01T00:00:00Z",
            CreatedAt: "2024-01-01T00:00:00Z"
        );
}
