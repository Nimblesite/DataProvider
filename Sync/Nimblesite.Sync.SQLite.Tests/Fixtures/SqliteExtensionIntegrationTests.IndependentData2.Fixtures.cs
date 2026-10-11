// Implements [CI-DESLOP-INDEPENDENT-DATA].

namespace Nimblesite.Sync.SQLite.Tests;

public partial class SqliteExtensionIntegrationTests
{
    private global::Nimblesite.Sync.Core.SyncSubscription Dataset_InsertSubscription_ValidSubscription_Succeeds_1() =>
        new SyncSubscription(
            SubscriptionId: "sub-1",
            OriginId: _originId,
            Type: SubscriptionType.Table,
            TableName: "Orders",
            Filter: null,
            CreatedAt: Timestamp,
            ExpiresAt: null
        );

    private global::Nimblesite.Sync.Core.SyncSubscription Dataset_GetAllSubscriptions_WithSubscriptions_ReturnsAll_1() =>
        new SyncSubscription(
            "sub-1",
            _originId,
            SubscriptionType.Table,
            "Orders",
            null,
            Timestamp,
            null
        );

    private global::Nimblesite.Sync.Core.SyncSubscription Dataset_GetAllSubscriptions_WithSubscriptions_ReturnsAll_2() =>
        new SyncSubscription(
            "sub-2",
            _originId,
            SubscriptionType.Record,
            "Products",
            "[\"p1\"]",
            Timestamp,
            "2025-12-31T23:59:59Z"
        );

    private global::Nimblesite.Sync.Core.SyncSubscription Dataset_GetSubscriptionsByTable_FiltersCorrectly_1() =>
        new SyncSubscription(
            "sub-orders",
            _originId,
            SubscriptionType.Table,
            "Orders",
            null,
            Timestamp,
            null
        );

    private global::Nimblesite.Sync.Core.SyncSubscription Dataset_GetSubscriptionsByTable_FiltersCorrectly_2() =>
        new SyncSubscription(
            "sub-products",
            _originId,
            SubscriptionType.Table,
            "Products",
            null,
            Timestamp,
            null
        );

    private global::Nimblesite.Sync.Core.SyncSubscription Dataset_DeleteSubscription_ExistingSubscription_Removes_1() =>
        new SyncSubscription(
            "sub-1",
            _originId,
            SubscriptionType.Table,
            "Orders",
            null,
            Timestamp,
            null
        );

    private static global::Nimblesite.Sync.Core.SyncSubscription Dataset_DeleteSubscriptionsByOrigin_RemovesAllForOrigin_1(
        global::System.String origin1
    ) =>
        new SyncSubscription(
            "sub-1",
            origin1,
            SubscriptionType.Table,
            "Orders",
            null,
            Timestamp,
            null
        );

    private static global::Nimblesite.Sync.Core.SyncSubscription Dataset_DeleteSubscriptionsByOrigin_RemovesAllForOrigin_2(
        global::System.String origin1
    ) =>
        new SyncSubscription(
            "sub-2",
            origin1,
            SubscriptionType.Table,
            "Products",
            null,
            Timestamp,
            null
        );

    private static global::Nimblesite.Sync.Core.SyncSubscription Dataset_DeleteSubscriptionsByOrigin_RemovesAllForOrigin_3(
        global::System.String origin2
    ) =>
        new SyncSubscription(
            "sub-3",
            origin2,
            SubscriptionType.Table,
            "Customers",
            null,
            Timestamp,
            null
        );

    private global::Nimblesite.Sync.Core.SyncSubscription Dataset_DeleteExpiredSubscriptions_RemovesExpired_1() =>
        new SyncSubscription(
            "sub-expired",
            _originId,
            SubscriptionType.Table,
            "Orders",
            null,
            "2025-01-01T00:00:00Z",
            "2025-06-01T00:00:00Z"
        );

    private global::Nimblesite.Sync.Core.SyncSubscription Dataset_DeleteExpiredSubscriptions_RemovesExpired_2() =>
        new SyncSubscription(
            "sub-active",
            _originId,
            SubscriptionType.Table,
            "Products",
            null,
            "2025-01-01T00:00:00Z",
            "2025-12-31T23:59:59Z"
        );

    private global::Nimblesite.Sync.Core.SyncSubscription Dataset_DeleteExpiredSubscriptions_RemovesExpired_3() =>
        new SyncSubscription(
            "sub-no-expiry",
            _originId,
            SubscriptionType.Table,
            "Customers",
            null,
            "2025-01-01T00:00:00Z",
            null
        );

    private global::Nimblesite.Sync.Core.SyncSubscription Dataset_InsertAndRetrieve_RecordSubscription_TypePreserved_1() =>
        new SyncSubscription(
            "sub-1",
            _originId,
            SubscriptionType.Record,
            "Orders",
            "[\"o1\"]",
            Timestamp,
            null
        );

    private global::Nimblesite.Sync.Core.SyncSubscription Dataset_InsertAndRetrieve_QuerySubscription_TypePreserved_1() =>
        new SyncSubscription(
            "sub-1",
            _originId,
            SubscriptionType.Query,
            "Orders",
            "{\"status\":\"pending\"}",
            Timestamp,
            null
        );

    private global::Nimblesite.Sync.Core.SyncSubscription Dataset_InsertAndRetrieve_WithExpiresAt_PreservesValue_1(
        global::System.String expiresAt
    ) =>
        new SyncSubscription(
            "sub-1",
            _originId,
            SubscriptionType.Table,
            "Orders",
            null,
            Timestamp,
            expiresAt
        );
}
