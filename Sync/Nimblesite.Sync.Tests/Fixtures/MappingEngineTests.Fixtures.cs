// Implements [CI-DESLOP-INDEPENDENT-DATA].
namespace Nimblesite.Sync.Tests;

public partial class MappingEngineTests
{
    private static global::Nimblesite.Sync.Core.TableMapping Fixture_FindMapping_ExactMatch_ReturnsMapping_1() =>
        MappingTestData.Mapping(
            id: "user-to-customer",
            sourceTable: "User",
            targetTable: "Customer",
            direction: MappingDirection.Push
        );

    private static global::Nimblesite.Sync.Core.TableMapping Fixture_FindMapping_BothDirection_MatchesPush_1() =>
        MappingTestData.Mapping(
            id: "user-both",
            sourceTable: "User",
            targetTable: "Customer",
            direction: MappingDirection.Both
        );

    private static global::Nimblesite.Sync.Core.TableMapping Fixture_FindMapping_BothDirection_MatchesPull_1() =>
        MappingTestData.Mapping(
            id: "user-both",
            sourceTable: "User",
            targetTable: "Customer",
            direction: MappingDirection.Both
        );

    private static global::Nimblesite.Sync.Core.TableMapping Fixture_FindMapping_WrongDirection_ReturnsNull_1() =>
        MappingTestData.Mapping(
            id: "user-push",
            sourceTable: "User",
            targetTable: "Customer",
            direction: MappingDirection.Push
        );

    private static global::Nimblesite.Sync.Core.TableMapping Fixture_FindMapping_UnmappedTable_ReturnsNull_1() =>
        MappingTestData.Mapping(
            id: "order-mapping",
            sourceTable: "Order",
            targetTable: "OrderSummary",
            direction: MappingDirection.Push
        );

    private static global::Nimblesite.Sync.Core.TableMapping Fixture_ApplyMapping_SingleTarget_RenamesTable_1() =>
        MappingTestData.Mapping(
            id: "user-to-customer",
            sourceTable: "User",
            targetTable: "Customer",
            direction: MappingDirection.Push
        );

    private static global::Nimblesite.Sync.Core.TableMapping Fixture_ApplyMapping_SingleTarget_MapsColumns_1(
        global::System.Collections.Generic.List<global::Nimblesite.Sync.Core.ColumnMapping> columnMappings
    ) =>
        MappingTestData.Mapping(
            id: "user-mapping",
            sourceTable: "User",
            targetTable: "Customer",
            columnMappings: columnMappings
        );

    private static global::Nimblesite.Sync.Core.TableMapping Fixture_ApplyMapping_SingleTarget_ConstantTransform_1(
        global::System.Collections.Generic.List<global::Nimblesite.Sync.Core.ColumnMapping> columnMappings
    ) =>
        MappingTestData.Mapping(
            id: "user-mapping",
            sourceTable: "User",
            targetTable: "Customer",
            columnMappings: columnMappings
        );

    private static global::Nimblesite.Sync.Core.TableMapping Fixture_ApplyMapping_DeleteOperation_NullPayload_Succeeds_1() =>
        MappingTestData.Mapping(
            id: "user-mapping",
            sourceTable: "User",
            targetTable: "Customer",
            direction: MappingDirection.Push
        );

    private static global::Nimblesite.Sync.Core.SyncLogEntry Fixture_ApplyMapping_DeleteOperation_NullPayload_Succeeds_2() =>
        new SyncLogEntry(
            Version: 1,
            TableName: "User",
            PkValue: """{"Id":"u1"}""",
            Operation: SyncOperation.Delete,
            Payload: null,
            Origin: "origin-1",
            Timestamp: "2024-01-01T00:00:00Z"
        );
}
