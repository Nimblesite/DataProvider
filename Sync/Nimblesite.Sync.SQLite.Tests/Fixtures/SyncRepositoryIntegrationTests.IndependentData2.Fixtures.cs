// Implements [CI-DESLOP-INDEPENDENT-DATA].

namespace Nimblesite.Sync.SQLite.Tests;

public partial class SyncRepositoryIntegrationTests
{
    private global::Nimblesite.Sync.Core.SyncSubscription Dataset_SubscriptionRepository_Insert_ValidSubscription_Succeeds_1() =>
        new SyncSubscription(
            "sub-1",
            _originId,
            SubscriptionType.Table,
            "Person",
            null,
            Timestamp,
            null
        );

    private global::Nimblesite.Sync.Core.SyncSubscription Dataset_SubscriptionRepository_GetById_ExistingSubscription_ReturnsSubscription_1() =>
        new SyncSubscription(
            "sub-1",
            _originId,
            SubscriptionType.Table,
            "Person",
            null,
            Timestamp,
            null
        );

    private global::Nimblesite.Sync.Core.SyncSubscription Dataset_SubscriptionRepository_GetByTable_FiltersCorrectly_1() =>
        new SyncSubscription(
            "sub-1",
            _originId,
            SubscriptionType.Table,
            "Person",
            null,
            Timestamp,
            null
        );

    private global::Nimblesite.Sync.Core.SyncSubscription Dataset_SubscriptionRepository_GetByTable_FiltersCorrectly_2() =>
        new SyncSubscription(
            "sub-2",
            _originId,
            SubscriptionType.Table,
            "Orders",
            null,
            Timestamp,
            null
        );

    private static global::Nimblesite.Sync.Core.SyncSubscription Dataset_SubscriptionRepository_GetByOrigin_FiltersCorrectly_1(
        global::System.String origin1
    ) =>
        new SyncSubscription(
            "sub-1",
            origin1,
            SubscriptionType.Table,
            "Person",
            null,
            Timestamp,
            null
        );

    private static global::Nimblesite.Sync.Core.SyncSubscription Dataset_SubscriptionRepository_GetByOrigin_FiltersCorrectly_2(
        global::System.String origin2
    ) =>
        new SyncSubscription(
            "sub-2",
            origin2,
            SubscriptionType.Table,
            "Person",
            null,
            Timestamp,
            null
        );

    private global::Nimblesite.Sync.Core.SyncSubscription Dataset_SubscriptionRepository_Delete_ExistingSubscription_Removes_1() =>
        new SyncSubscription(
            "sub-1",
            _originId,
            SubscriptionType.Table,
            "Person",
            null,
            Timestamp,
            null
        );

    private static global::Nimblesite.Sync.Core.SyncSubscription Dataset_SubscriptionRepository_DeleteByOrigin_RemovesAll_1(
        global::System.String origin1
    ) =>
        new SyncSubscription(
            "sub-1",
            origin1,
            SubscriptionType.Table,
            "Person",
            null,
            Timestamp,
            null
        );

    private static global::Nimblesite.Sync.Core.SyncSubscription Dataset_SubscriptionRepository_DeleteByOrigin_RemovesAll_2(
        global::System.String origin1
    ) =>
        new SyncSubscription(
            "sub-2",
            origin1,
            SubscriptionType.Table,
            "Orders",
            null,
            Timestamp,
            null
        );

    private static global::Nimblesite.Sync.Core.SyncSubscription Dataset_SubscriptionRepository_DeleteByOrigin_RemovesAll_3() =>
        new SyncSubscription(
            "sub-3",
            "origin-2",
            SubscriptionType.Table,
            "Products",
            null,
            Timestamp,
            null
        );

    private global::Nimblesite.Sync.Core.SyncSubscription Dataset_SubscriptionRepository_DeleteExpired_RemovesExpiredOnly_1() =>
        new SyncSubscription(
            "sub-expired",
            _originId,
            SubscriptionType.Table,
            "Person",
            null,
            "2025-01-01T00:00:00Z",
            "2025-06-01T00:00:00Z"
        );

    private global::Nimblesite.Sync.Core.SyncSubscription Dataset_SubscriptionRepository_DeleteExpired_RemovesExpiredOnly_2() =>
        new SyncSubscription(
            "sub-active",
            _originId,
            SubscriptionType.Table,
            "Orders",
            null,
            "2025-01-01T00:00:00Z",
            "2025-12-31T23:59:59Z"
        );

    private static global::System.Collections.Generic.List<global::Nimblesite.Sync.Core.SyncClient> Dataset_TombstoneManager_CalculateSafeVersion_WithClients_ReturnsMinVersion_1() =>
        new List<SyncClient>
        {
            new("client-1", 100, Timestamp, Timestamp),
            new("client-2", 200, Timestamp, Timestamp),
            new("client-3", 300, Timestamp, Timestamp),
        };
}
