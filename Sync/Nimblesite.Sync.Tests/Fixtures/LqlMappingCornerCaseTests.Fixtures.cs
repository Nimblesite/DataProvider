// Implements [CI-DESLOP-INDEPENDENT-DATA].
using Microsoft.Extensions.Logging.Abstractions;
using static Nimblesite.Sync.Tests.MappingTestData;

namespace Nimblesite.Sync.Tests;

public partial class LqlMappingCornerCaseTests
{
    private global::Nimblesite.Sync.Core.TableMapping Fixture_MappingEngine_NullPayload_ReturnsNullMappedPayload_1(
        global::System.String id,
        global::System.String sourceTable,
        global::System.String? targetTable,
        global::System.Collections.Generic.IReadOnlyList<global::Nimblesite.Sync.Core.ColumnMapping>? columnMappings
    ) =>
        MappingTestData.Mapping(
            id: "test",
            sourceTable: "Source",
            targetTable: "Target",
            columnMappings:
            [
                new ColumnMapping("Name", "DisplayName", TransformType.Lql, null, "upper(Name)"),
            ]
        );

    private global::Nimblesite.Sync.Core.SyncLogEntry Fixture_MappingEngine_NullPayload_ReturnsNullMappedPayload_2(
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
            TableName: "Source",
            PkValue: """{"Id":"1"}""",
            Operation: SyncOperation.Delete,
            Payload: null, // DELETE operations have null payload
            Origin: "test",
            Timestamp: "2024-01-01T00:00:00Z"
        );

    private global::Nimblesite.Sync.Core.TableMapping Fixture_MappingEngine_LqlWithMissingSourceColumn_FallsBack_1(
        global::System.String id,
        global::System.String sourceTable,
        global::System.String? targetTable,
        global::System.Collections.Generic.IReadOnlyList<global::Nimblesite.Sync.Core.ColumnMapping>? columnMappings,
        global::System.Collections.Generic.List<global::Nimblesite.Sync.Core.ColumnMapping> columnMappings
    ) =>
        MappingTestData.Mapping(
            id: "test",
            sourceTable: "Source",
            targetTable: "Target",
            columnMappings: columnMappings
        );

    private global::Nimblesite.Sync.Core.TableMapping Fixture_MappingEngine_MixedTransformTypes_AllApplied_1(
        global::System.String id,
        global::System.String sourceTable,
        global::System.String? targetTable,
        global::System.Collections.Generic.IReadOnlyList<global::Nimblesite.Sync.Core.ColumnMapping>? columnMappings,
        global::System.Collections.Generic.List<global::Nimblesite.Sync.Core.ColumnMapping> columnMappings
    ) =>
        MappingTestData.Mapping(
            id: "test",
            sourceTable: "Source",
            targetTable: "Target",
            columnMappings: columnMappings
        );

    private global::Nimblesite.Sync.Core.ColumnMapping Fixture_MappingEngine_MultiTarget_AllTargetsReceiveTransformedData_1() =>
        new ColumnMapping("Name", "DisplayName", TransformType.Lql, null, "upper(Name)");

    private global::Nimblesite.Sync.Core.TableMapping Fixture_E2E_FullAddressNormalization_1(
        global::System.String id,
        global::System.String sourceTable,
        global::System.String? targetTable,
        global::System.Collections.Generic.IReadOnlyList<global::Nimblesite.Sync.Core.ColumnMapping>? columnMappings,
        global::System.Collections.Generic.List<global::Nimblesite.Sync.Core.ColumnMapping> columnMappings
    ) =>
        MappingTestData.Mapping(
            id: "address-norm",
            sourceTable: "RawAddress",
            targetTable: "Address",
            columnMappings: columnMappings
        );

    private global::Nimblesite.Sync.Core.TableMapping Fixture_E2E_UserDataSanitization_1(
        global::System.String id,
        global::System.String sourceTable,
        global::System.String? targetTable,
        global::System.Collections.Generic.IReadOnlyList<global::Nimblesite.Sync.Core.ColumnMapping>? columnMappings,
        global::System.Collections.Generic.List<global::Nimblesite.Sync.Core.ColumnMapping> columnMappings
    ) =>
        MappingTestData.Mapping(
            id: "user-sanitize",
            sourceTable: "LegacyUser",
            targetTable: "CleanUser",
            columnMappings: columnMappings
        );
}
