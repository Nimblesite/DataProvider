// Implements [CI-DESLOP-INDEPENDENT-DATA].
namespace Nimblesite.Sync.SQLite.Tests;

public partial class SubscriptionIntegrationTests
{
    private static global::Nimblesite.Sync.Core.SyncLogEntry Fixture_Spec10_4_CreateNotifications_IncludesFullChangePayload_1() =>
        new SyncLogEntry(
            Version: 100,
            TableName: "Orders",
            PkValue: "{\"Id\":\"order-123\"}",
            Operation: SyncOperation.Update,
            Payload: "{\"Id\":\"order-123\",\"Status\":\"shipped\",\"Total\":99.99}",
            Origin: "server-origin",
            Timestamp: Timestamp
        );

    private static global::Nimblesite.Sync.Core.SyncLogEntry Fixture_Spec10_7_Notifications_IncludeFullPayload_1() =>
        new SyncLogEntry(
            1,
            "Orders",
            "{\"Id\":\"o1\"}",
            SyncOperation.Update,
            "{\"Id\":\"o1\",\"Status\":\"shipped\",\"Amount\":150.00}",
            "server",
            Timestamp
        );
}
