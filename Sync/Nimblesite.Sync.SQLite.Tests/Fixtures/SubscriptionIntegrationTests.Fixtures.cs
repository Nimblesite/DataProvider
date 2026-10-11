// Implements [CI-DESLOP-INDEPENDENT-DATA].
using Microsoft.Data.Sqlite;
using Xunit;

namespace Nimblesite.Sync.SQLite.Tests;

public partial class SubscriptionIntegrationTests
{
    private global::Nimblesite.Sync.Core.SyncLogEntry Fixture_Spec10_4_CreateNotifications_IncludesFullChangePayload_1(
        global::System.Int64 Version,
        global::System.String TableName,
        global::System.String PkValue,
        global::Nimblesite.Sync.Core.SyncOperation Operation,
        global::System.String? Payload,
        global::System.String Origin,
        global::System.String Timestamp
    ) =>
        new SyncLogEntry(
            Version: 100,
            TableName: "Orders",
            PkValue: "{\"Id\":\"order-123\"}",
            Operation: SyncOperation.Update,
            Payload: "{\"Id\":\"order-123\",\"Status\":\"shipped\",\"Total\":99.99}",
            Origin: "server-origin",
            Timestamp: Timestamp
        );

    private global::Nimblesite.Sync.Core.SyncLogEntry Fixture_Spec10_7_Notifications_IncludeFullPayload_1() =>
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
