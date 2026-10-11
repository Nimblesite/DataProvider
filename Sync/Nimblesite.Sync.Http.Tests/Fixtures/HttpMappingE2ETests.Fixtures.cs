// Implements [CI-DESLOP-INDEPENDENT-DATA].
using System.Text.Json;
using Tests.Shared;

namespace Nimblesite.Sync.Http.Tests;

public partial class HttpMappingE2ETests
{
    private global::Nimblesite.Sync.Core.SyncLogEntry Fixture_MappingEngine_TransformsUserToCustomer_WithLql_1(
        global::System.Int64 Version,
        global::System.String TableName,
        global::System.String PkValue,
        global::Nimblesite.Sync.Core.SyncOperation Operation,
        global::System.String? Payload,
        global::System.String Origin,
        global::System.String Timestamp
    ) =>
        new SyncLogEntry(
            Version: 1,
            TableName: "User",
            PkValue: """{"Id":"u123"}""",
            Operation: SyncOperation.Insert,
            Payload: """{"Id":"u123","FullName":"Alice Smith","EmailAddress":"alice@example.com","PasswordHash":"secret","SecurityStamp":"xyz","CreatedAt":"2024-06-15T10:30:00Z"}""",
            Origin: "source-origin",
            Timestamp: "2024-06-15T10:30:00Z"
        );

    private global::Nimblesite.Sync.Core.SyncLogEntry Fixture_E2E_SyncWithMapping_TransformsData_AcrossDatabases_1(
        global::System.Int64 Version,
        global::System.Collections.Generic.IReadOnlyList<global::Nimblesite.Sync.Core.SyncLogEntry> changesList,
        global::System.String TableName,
        global::Nimblesite.Sync.Core.MappedEntry entry,
        global::System.String PkValue,
        global::Nimblesite.Sync.Core.SyncOperation Operation,
        global::System.String? Payload,
        global::System.String mergedPayload,
        global::System.String Origin,
        global::System.String Timestamp
    ) =>
        new SyncLogEntry(
            Version: changesList[0].Version,
            TableName: entry.TargetTable,
            PkValue: entry.TargetPkValue,
            Operation: changesList[0].Operation,
            Payload: mergedPayload,
            Origin: changesList[0].Origin,
            Timestamp: changesList[0].Timestamp
        );

    private global::Nimblesite.Sync.Core.SyncLogEntry Fixture_E2E_MultipleRecords_AllTransformedCorrectly_1(
        global::Nimblesite.Sync.Core.SyncLogEntry change,
        global::Nimblesite.Sync.Core.MappedEntry entry,
        global::System.String mergedPayload
    ) =>
        new SyncLogEntry(
            change.Version,
            entry.TargetTable,
            entry.TargetPkValue,
            change.Operation,
            mergedPayload,
            change.Origin,
            change.Timestamp
        );

    private global::Nimblesite.Sync.Core.SyncLogEntry Fixture_MappingEngine_DeleteOperation_TransformsTableAndPk_1(
        global::System.Int64 Version,
        global::System.String TableName,
        global::System.String PkValue,
        global::Nimblesite.Sync.Core.SyncOperation Operation,
        global::System.String? Payload,
        global::System.String Origin,
        global::System.String Timestamp
    ) =>
        new SyncLogEntry(
            Version: 1,
            TableName: "User",
            PkValue: """{"Id":"u999"}""",
            Operation: SyncOperation.Delete,
            Payload: null, // Deletes have no payload
            Origin: "test",
            Timestamp: "2024-01-01T00:00:00Z"
        );

    private global::Nimblesite.Sync.Core.SyncLogEntry Fixture_MappingEngine_UpdateOperation_TransformsPayload_1(
        global::System.Int64 Version,
        global::System.String TableName,
        global::System.String PkValue,
        global::Nimblesite.Sync.Core.SyncOperation Operation,
        global::System.String? Payload,
        global::System.String Origin,
        global::System.String Timestamp
    ) =>
        new SyncLogEntry(
            Version: 2,
            TableName: "User",
            PkValue: """{"Id":"u888"}""",
            Operation: SyncOperation.Update,
            Payload: """{"Id":"u888","FullName":"Updated Name","EmailAddress":"new@example.com","PasswordHash":"newhash","SecurityStamp":"newstamp","CreatedAt":"2024-01-01T00:00:00Z"}""",
            Origin: "test",
            Timestamp: "2024-01-02T00:00:00Z"
        );

    private global::Nimblesite.Sync.Core.SyncMappingConfig Fixture_MappingEngine_DisabledMapping_ReturnsSkipped_1(
        global::System.String Version,
        global::Nimblesite.Sync.Core.UnmappedTableBehavior UnmappedTableBehavior,
        global::System.Collections.Generic.IReadOnlyList<global::Nimblesite.Sync.Core.TableMapping> Mappings,
        global::System.String Id,
        global::System.String SourceTable,
        global::System.String? TargetTable,
        global::Nimblesite.Sync.Core.MappingDirection Direction,
        global::System.Boolean Enabled,
        global::Nimblesite.Sync.Core.PkMapping? PkMapping,
        global::System.Collections.Generic.IReadOnlyList<global::Nimblesite.Sync.Core.ColumnMapping> ColumnMappings,
        global::System.Collections.Generic.IReadOnlyList<global::System.String> ExcludedColumns,
        global::Nimblesite.Sync.Core.SyncFilter? Filter,
        global::Nimblesite.Sync.Core.SyncTrackingConfig SyncTracking
    ) =>
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

    private global::Nimblesite.Sync.Core.SyncMappingConfig Fixture_MappingEngine_WrongDirection_ReturnsSkipped_1(
        global::System.String Version,
        global::Nimblesite.Sync.Core.UnmappedTableBehavior UnmappedTableBehavior,
        global::System.Collections.Generic.IReadOnlyList<global::Nimblesite.Sync.Core.TableMapping> Mappings,
        global::System.String Id,
        global::System.String SourceTable,
        global::System.String? TargetTable,
        global::Nimblesite.Sync.Core.MappingDirection Direction,
        global::System.Boolean Enabled,
        global::Nimblesite.Sync.Core.PkMapping? PkMapping,
        global::System.Collections.Generic.IReadOnlyList<global::Nimblesite.Sync.Core.ColumnMapping> ColumnMappings,
        global::System.Collections.Generic.IReadOnlyList<global::System.String> ExcludedColumns,
        global::Nimblesite.Sync.Core.SyncFilter? Filter,
        global::Nimblesite.Sync.Core.SyncTrackingConfig SyncTracking
    ) =>
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

    private global::Nimblesite.Sync.Core.SyncMappingConfig Fixture_MappingEngine_BothDirection_WorksForPushAndPull_1(
        global::System.String Version,
        global::Nimblesite.Sync.Core.UnmappedTableBehavior UnmappedTableBehavior,
        global::System.Collections.Generic.IReadOnlyList<global::Nimblesite.Sync.Core.TableMapping> Mappings,
        global::System.String Id,
        global::System.String SourceTable,
        global::System.String? TargetTable,
        global::Nimblesite.Sync.Core.MappingDirection Direction,
        global::System.Boolean Enabled,
        global::Nimblesite.Sync.Core.PkMapping? PkMapping,
        global::System.Collections.Generic.IReadOnlyList<global::Nimblesite.Sync.Core.ColumnMapping> ColumnMappings,
        global::System.Collections.Generic.IReadOnlyList<global::System.String> ExcludedColumns,
        global::Nimblesite.Sync.Core.SyncFilter? Filter,
        global::Nimblesite.Sync.Core.SyncTrackingConfig SyncTracking
    ) =>
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

    private global::Nimblesite.Sync.Core.ColumnMapping Fixture_MappingEngine_MultipleColumnMappings_AllApplied_1(
        global::System.String? Source,
        global::System.String Target,
        global::Nimblesite.Sync.Core.TransformType Transform,
        global::System.String? Value
    ) =>
        new ColumnMapping(
            Source: null,
            Target: "constant",
            Transform: TransformType.Constant,
            Value: "fixed"
        );

    private global::Nimblesite.Sync.Core.SyncMappingConfig Fixture_MappingEngine_ExcludeMultipleColumns_AllExcluded_1(
        global::System.String Version,
        global::Nimblesite.Sync.Core.UnmappedTableBehavior UnmappedTableBehavior,
        global::System.Collections.Generic.IReadOnlyList<global::Nimblesite.Sync.Core.TableMapping> Mappings,
        global::System.String Id,
        global::System.String SourceTable,
        global::System.String? TargetTable,
        global::Nimblesite.Sync.Core.MappingDirection Direction,
        global::System.Boolean Enabled,
        global::Nimblesite.Sync.Core.PkMapping? PkMapping,
        global::System.Collections.Generic.IReadOnlyList<global::Nimblesite.Sync.Core.ColumnMapping> ColumnMappings,
        global::System.Collections.Generic.IReadOnlyList<global::System.String> ExcludedColumns,
        global::Nimblesite.Sync.Core.SyncFilter? Filter,
        global::Nimblesite.Sync.Core.SyncTrackingConfig SyncTracking
    ) =>
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
