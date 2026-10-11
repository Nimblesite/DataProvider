// Implements [CI-DESLOP-INDEPENDENT-DATA].
using Microsoft.Data.Sqlite;
using Tests.Shared;
using Xunit;

namespace Nimblesite.Sync.SQLite.Tests;

public partial class ChangeApplierIntegrationTests
{
    private static global::Nimblesite.Sync.Core.SyncLogEntry Fixture_ApplyChange_Insert_CreatesNewRecord_1() =>
        new SyncLogEntry(
            Version: 1,
            TableName: "Person",
            PkValue: "{\"Id\":\"p1\"}",
            Operation: SyncOperation.Insert,
            Payload: "{\"Id\":\"p1\",\"Name\":\"Alice\",\"Age\":30,\"Email\":\"alice@example.com\"}",
            Origin: "remote-origin",
            Timestamp: Timestamp
        );

    private static global::Nimblesite.Sync.Core.SyncLogEntry Fixture_ApplyChange_Insert_WithNullPayload_ReturnsError_1() =>
        new SyncLogEntry(
            Version: 1,
            TableName: "Person",
            PkValue: "{\"Id\":\"p1\"}",
            Operation: SyncOperation.Insert,
            Payload: null,
            Origin: "remote-origin",
            Timestamp: Timestamp
        );

    private static global::Nimblesite.Sync.Core.SyncLogEntry Fixture_ApplyChange_Insert_WithEmptyPayload_ReturnsError_1() =>
        new SyncLogEntry(
            Version: 1,
            TableName: "Person",
            PkValue: "{\"Id\":\"p1\"}",
            Operation: SyncOperation.Insert,
            Payload: "{}",
            Origin: "remote-origin",
            Timestamp: Timestamp
        );

    private static global::Nimblesite.Sync.Core.SyncLogEntry Fixture_ApplyChange_Insert_DuplicateKey_ReplacesExisting_1() =>
        new SyncLogEntry(
            Version: 1,
            TableName: "Person",
            PkValue: "{\"Id\":\"p1\"}",
            Operation: SyncOperation.Insert,
            Payload: "{\"Id\":\"p1\",\"Name\":\"Alice\",\"Age\":30}",
            Origin: "remote-origin",
            Timestamp: Timestamp
        );

    private static global::Nimblesite.Sync.Core.SyncLogEntry Fixture_ApplyChange_Insert_DuplicateKey_ReplacesExisting_2() =>
        new SyncLogEntry(
            Version: 2,
            TableName: "Person",
            PkValue: "{\"Id\":\"p1\"}",
            Operation: SyncOperation.Insert,
            Payload: "{\"Id\":\"p1\",\"Name\":\"Alice Updated\",\"Age\":31}",
            Origin: "remote-origin",
            Timestamp: Timestamp
        );

    private static global::Nimblesite.Sync.Core.SyncLogEntry Fixture_ApplyChange_Update_ModifiesExistingRecord_1() =>
        new SyncLogEntry(
            Version: 2,
            TableName: "Person",
            PkValue: "{\"Id\":\"p1\"}",
            Operation: SyncOperation.Update,
            Payload: "{\"Id\":\"p1\",\"Name\":\"Alice Updated\",\"Age\":31,\"Email\":\"alice.new@example.com\"}",
            Origin: "remote-origin",
            Timestamp: Timestamp
        );

    private static global::Nimblesite.Sync.Core.SyncLogEntry Fixture_ApplyChange_Update_NonExistingRecord_InsertsAsUpsert_1() =>
        new SyncLogEntry(
            Version: 1,
            TableName: "Person",
            PkValue: "{\"Id\":\"p1\"}",
            Operation: SyncOperation.Update,
            Payload: "{\"Id\":\"p1\",\"Name\":\"Alice\",\"Age\":30}",
            Origin: "remote-origin",
            Timestamp: Timestamp
        );

    private static global::Nimblesite.Sync.Core.SyncLogEntry Fixture_ApplyChange_Delete_RemovesExistingRecord_1() =>
        new SyncLogEntry(
            Version: 2,
            TableName: "Person",
            PkValue: "{\"Id\":\"p1\"}",
            Operation: SyncOperation.Delete,
            Payload: null,
            Origin: "remote-origin",
            Timestamp: Timestamp
        );

    private static global::Nimblesite.Sync.Core.SyncLogEntry Fixture_ApplyChange_Delete_NonExistingRecord_StillSucceeds_1() =>
        new SyncLogEntry(
            Version: 1,
            TableName: "Person",
            PkValue: "{\"Id\":\"nonexistent\"}",
            Operation: SyncOperation.Delete,
            Payload: null,
            Origin: "remote-origin",
            Timestamp: Timestamp
        );

    private static global::Nimblesite.Sync.Core.SyncLogEntry Fixture_ApplyChange_Delete_InvalidPkValue_ReturnsError_1() =>
        new SyncLogEntry(
            Version: 1,
            TableName: "Person",
            PkValue: "{}",
            Operation: SyncOperation.Delete,
            Payload: null,
            Origin: "remote-origin",
            Timestamp: Timestamp
        );

    private static global::Nimblesite.Sync.Core.SyncLogEntry Fixture_ApplyChange_Insert_HandlesStringValues_1() =>
        new SyncLogEntry(
            Version: 1,
            TableName: "Person",
            PkValue: "{\"Id\":\"p1\"}",
            Operation: SyncOperation.Insert,
            Payload: "{\"Id\":\"p1\",\"Name\":\"Alice\",\"Email\":\"alice@example.com\"}",
            Origin: "remote-origin",
            Timestamp: Timestamp
        );

    private static global::Nimblesite.Sync.Core.SyncLogEntry Fixture_ApplyChange_Insert_HandlesIntegerValues_1() =>
        new SyncLogEntry(
            Version: 1,
            TableName: "Person",
            PkValue: "{\"Id\":\"p1\"}",
            Operation: SyncOperation.Insert,
            Payload: "{\"Id\":\"p1\",\"Name\":\"Alice\",\"Age\":25}",
            Origin: "remote-origin",
            Timestamp: Timestamp
        );

    private static global::Nimblesite.Sync.Core.SyncLogEntry Fixture_ApplyChange_Insert_HandlesNullValues_1() =>
        new SyncLogEntry(
            Version: 1,
            TableName: "Person",
            PkValue: "{\"Id\":\"p1\"}",
            Operation: SyncOperation.Insert,
            Payload: "{\"Id\":\"p1\",\"Name\":\"Alice\",\"Email\":null}",
            Origin: "remote-origin",
            Timestamp: Timestamp
        );

    private static global::Nimblesite.Sync.Core.SyncLogEntry Fixture_ApplyChange_Insert_HandlesBooleanValues_1() =>
        new SyncLogEntry(
            Version: 1,
            TableName: "Settings",
            PkValue: "{\"Id\":\"s1\"}",
            Operation: SyncOperation.Insert,
            Payload: "{\"Id\":\"s1\",\"Enabled\":true}",
            Origin: "remote-origin",
            Timestamp: Timestamp
        );

    private static global::Nimblesite.Sync.Core.SyncLogEntry Fixture_ApplyChange_Insert_HandlesFalseBooleanValues_1() =>
        new SyncLogEntry(
            Version: 1,
            TableName: "Settings",
            PkValue: "{\"Id\":\"s1\"}",
            Operation: SyncOperation.Insert,
            Payload: "{\"Id\":\"s1\",\"Enabled\":false}",
            Origin: "remote-origin",
            Timestamp: Timestamp
        );

    private static global::Nimblesite.Sync.Core.SyncLogEntry Fixture_ApplyChange_Insert_HandlesDoubleValues_1() =>
        new SyncLogEntry(
            Version: 1,
            TableName: "Product",
            PkValue: "{\"Id\":\"prod1\"}",
            Operation: SyncOperation.Insert,
            Payload: "{\"Id\":\"prod1\",\"Price\":99.99}",
            Origin: "remote-origin",
            Timestamp: Timestamp
        );

    private static global::Nimblesite.Sync.Core.SyncLogEntry Fixture_ApplyChange_Insert_WithForeignKeyViolation_ReturnsFalse_1() =>
        new SyncLogEntry(
            Version: 1,
            TableName: "Employee",
            PkValue: "{\"Id\":\"e1\"}",
            Operation: SyncOperation.Insert,
            Payload: "{\"Id\":\"e1\",\"Name\":\"John\",\"DeptId\":\"nonexistent\"}",
            Origin: "remote-origin",
            Timestamp: Timestamp
        );

    private static global::Nimblesite.Sync.Core.SyncLogEntry Fixture_ApplyChange_Insert_WithValidForeignKey_Succeeds_1() =>
        new SyncLogEntry(
            Version: 1,
            TableName: "Employee",
            PkValue: "{\"Id\":\"e1\"}",
            Operation: SyncOperation.Insert,
            Payload: "{\"Id\":\"e1\",\"Name\":\"John\",\"DeptId\":\"d1\"}",
            Origin: "remote-origin",
            Timestamp: Timestamp
        );

    private static global::Nimblesite.Sync.Core.SyncLogEntry Fixture_ApplyChange_MultipleBatches_AllSucceed_1() =>
        new SyncLogEntry(
                1,
                "Person",
                "{\"Id\":\"p1\"}",
                SyncOperation.Insert,
                "{\"Id\":\"p1\",\"Name\":\"Alice\"}",
                "origin",
                Timestamp
            );

    private static global::Nimblesite.Sync.Core.SyncLogEntry Fixture_ApplyChange_MultipleBatches_AllSucceed_2() =>
        new SyncLogEntry(
                2,
                "Person",
                "{\"Id\":\"p2\"}",
                SyncOperation.Insert,
                "{\"Id\":\"p2\",\"Name\":\"Bob\"}",
                "origin",
                Timestamp
            );

    private static global::Nimblesite.Sync.Core.SyncLogEntry Fixture_ApplyChange_MultipleBatches_AllSucceed_3() =>
        new SyncLogEntry(
                3,
                "Person",
                "{\"Id\":\"p3\"}",
                SyncOperation.Insert,
                "{\"Id\":\"p3\",\"Name\":\"Charlie\"}",
                "origin",
                Timestamp
            );
}
