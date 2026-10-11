// Implements [CI-DESLOP-INDEPENDENT-DATA].
using Tests.Shared;

namespace Nimblesite.Sync.Http.Tests;

public partial class HttpMappingE2ETests
{
    private static global::Nimblesite.Sync.Core.SyncLogEntry Fixture_MappingEngine_TransformsUserToCustomer_WithLql_1() =>
        new SyncLogEntry(
            Version: 1,
            TableName: "User",
            PkValue: """{"Id":"u123"}""",
            Operation: SyncOperation.Insert,
            Payload: """{"Id":"u123","FullName":"Alice Smith","EmailAddress":"alice@example.com","PasswordHash":"secret","SecurityStamp":"xyz","CreatedAt":"2024-06-15T10:30:00Z"}""",
            Origin: "source-origin",
            Timestamp: "2024-06-15T10:30:00Z"
        );

    private static global::Nimblesite.Sync.Core.SyncLogEntry Fixture_E2E_SyncWithMapping_TransformsData_AcrossDatabases_1(global::System.Collections.Generic.IReadOnlyList<global::Nimblesite.Sync.Core.SyncLogEntry> changesList, global::Nimblesite.Sync.Core.MappedEntry entry, global::System.String mergedPayload) =>
        new SyncLogEntry(
                Version: changesList[0].Version,
                TableName: entry.TargetTable,
                PkValue: entry.TargetPkValue,
                Operation: changesList[0].Operation,
                Payload: mergedPayload,
                Origin: changesList[0].Origin,
                Timestamp: changesList[0].Timestamp
            );

    private static global::Nimblesite.Sync.Core.SyncLogEntry Fixture_E2E_MultipleRecords_AllTransformedCorrectly_1(global::Nimblesite.Sync.Core.SyncLogEntry change, global::Nimblesite.Sync.Core.MappedEntry entry, global::System.String mergedPayload) =>
        new SyncLogEntry(
                    change.Version,
                    entry.TargetTable,
                    entry.TargetPkValue,
                    change.Operation,
                    mergedPayload,
                    change.Origin,
                    change.Timestamp
                );

    private static global::Nimblesite.Sync.Core.SyncLogEntry Fixture_MappingEngine_DeleteOperation_TransformsTableAndPk_1() =>
        new SyncLogEntry(
            Version: 1,
            TableName: "User",
            PkValue: """{"Id":"u999"}""",
            Operation: SyncOperation.Delete,
            Payload: null, // Deletes have no payload
            Origin: "test",
            Timestamp: "2024-01-01T00:00:00Z"
        );

    private static global::Nimblesite.Sync.Core.SyncLogEntry Fixture_MappingEngine_UpdateOperation_TransformsPayload_1() =>
        new SyncLogEntry(
            Version: 2,
            TableName: "User",
            PkValue: """{"Id":"u888"}""",
            Operation: SyncOperation.Update,
            Payload: """{"Id":"u888","FullName":"Updated Name","EmailAddress":"new@example.com","PasswordHash":"newhash","SecurityStamp":"newstamp","CreatedAt":"2024-01-01T00:00:00Z"}""",
            Origin: "test",
            Timestamp: "2024-01-02T00:00:00Z"
        );

    private static global::Nimblesite.Sync.Core.SyncMappingConfig Fixture_MappingEngine_DisabledMapping_ReturnsSkipped_1() =>
        new SyncMappingConfig(
            Version: "1.0",
            UnmappedTableBehavior: UnmappedTableBehavior.Strict,
            Mappings:
            [
                new TableMapping(
                    Id: "disabled-test",
                    SourceTable: "Source",
                    TargetTable: "target",
                    Direction: MappingDirection.Push,
                    Enabled: false, // DISABLED
                    PkMapping: null,
                    ColumnMappings: [],
                    ExcludedColumns: [],
                    Filter: null,
                    SyncTracking: new SyncTrackingConfig()
                ),
            ]
        );

    private static global::Nimblesite.Sync.Core.SyncMappingConfig Fixture_MappingEngine_WrongDirection_ReturnsSkipped_1() =>
        new SyncMappingConfig(
            Version: "1.0",
            UnmappedTableBehavior: UnmappedTableBehavior.Strict,
            Mappings:
            [
                new TableMapping(
                    Id: "pull-only",
                    SourceTable: "Source",
                    TargetTable: "target",
                    Direction: MappingDirection.Pull, // Pull only
                    Enabled: true,
                    PkMapping: null,
                    ColumnMappings: [],
                    ExcludedColumns: [],
                    Filter: null,
                    SyncTracking: new SyncTrackingConfig()
                ),
            ]
        );

    private static global::Nimblesite.Sync.Core.SyncMappingConfig Fixture_MappingEngine_BothDirection_WorksForPushAndPull_1() =>
        new SyncMappingConfig(
            Version: "1.0",
            UnmappedTableBehavior: UnmappedTableBehavior.Strict,
            Mappings:
            [
                new TableMapping(
                    Id: "both-dir",
                    SourceTable: "Source",
                    TargetTable: "target",
                    Direction: MappingDirection.Both,
                    Enabled: true,
                    PkMapping: null,
                    ColumnMappings: [new ColumnMapping("Data", "data")],
                    ExcludedColumns: [],
                    Filter: null,
                    SyncTracking: new SyncTrackingConfig()
                ),
            ]
        );

    private static global::Nimblesite.Sync.Core.ColumnMapping Fixture_MappingEngine_MultipleColumnMappings_AllApplied_1() =>
        new ColumnMapping(
                Source: null,
                Target: "constant",
                Transform: TransformType.Constant,
                Value: "fixed"
            );

    private static global::Nimblesite.Sync.Core.SyncMappingConfig Fixture_MappingEngine_ExcludeMultipleColumns_AllExcluded_1() =>
        new SyncMappingConfig(
            Version: "1.0",
            UnmappedTableBehavior: UnmappedTableBehavior.Strict,
            Mappings:
            [
                new TableMapping(
                    Id: "exclude-test",
                    SourceTable: "Source",
                    TargetTable: "target",
                    Direction: MappingDirection.Push,
                    Enabled: true,
                    PkMapping: null,
                    ColumnMappings: [new ColumnMapping("Name", "name")],
                    ExcludedColumns: ["Password", "Salt", "Token", "Secret", "PrivateKey"],
                    Filter: null,
                    SyncTracking: new SyncTrackingConfig()
                ),
            ]
        );
}
