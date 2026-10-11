// Implements [CI-DESLOP-INDEPENDENT-DATA].

namespace Nimblesite.Sync.Tests;

public partial class SubscriptionManagerTests
{
    private static global::Nimblesite.Sync.Core.SyncSubscription Dataset_MatchesChange_RecordSubscription_NullFilter_ReturnsFalse_1() =>
        new SyncSubscription(
            "sub-1",
            "origin-1",
            SubscriptionType.Record,
            "Person",
            null,
            Timestamp,
            null
        );

    private static global::Nimblesite.Sync.Core.SyncSubscription Dataset_MatchesChange_RecordSubscription_EmptyFilter_ReturnsFalse_1() =>
        new SyncSubscription(
            "sub-1",
            "origin-1",
            SubscriptionType.Record,
            "Person",
            "",
            Timestamp,
            null
        );
}
