// Implements [CI-DESLOP-INDEPENDENT-DATA].

namespace Nimblesite.Sync.Tests;

public partial class MappingEngineTests
{
    private static global::Nimblesite.Sync.Core.SyncLogEntry Dataset_ApplyMapping_StrictMode_NoMapping_ReturnsSkipped_1() =>
        MappingTestData.Entry(
            table: "Person",
            pk: """{"Id":"p1"}""",
            payload: """{"Id":"p1","Name":"Alice"}"""
        );

    private static global::Nimblesite.Sync.Core.TableMapping Dataset_ApplyMapping_SingleTarget_MapsPrimaryKey_1() =>
        MappingTestData.Mapping(id: "user-mapping", sourceTable: "User", targetTable: "Customer");

    private static global::System.Collections.Generic.List<global::Nimblesite.Sync.Core.ColumnMapping> Dataset_ApplyMapping_SingleTarget_MapsColumns_1() =>
        new List<ColumnMapping> { new("FullName", "Name"), new("EmailAddress", "Email") };

    private static global::Nimblesite.Sync.Core.TableMapping Dataset_ApplyMapping_SingleTarget_MapsColumns_2(
        global::System.Collections.Generic.List<global::Nimblesite.Sync.Core.ColumnMapping> columnMappings
    ) => Fixture_ApplyMapping_SingleTarget_MapsColumns_1(columnMappings: columnMappings);

    private static global::System.Collections.Generic.List<global::Nimblesite.Sync.Core.ColumnMapping> Dataset_ApplyMapping_SingleTarget_ConstantTransform_1() =>
        new List<ColumnMapping>
        {
            new("Name", "Name"),
            new(null, "Source", TransformType.Constant, "mobile-app"),
        };

    private static global::Nimblesite.Sync.Core.TableMapping Dataset_ApplyMapping_SingleTarget_ConstantTransform_2(
        global::System.Collections.Generic.List<global::Nimblesite.Sync.Core.ColumnMapping> columnMappings
    ) => Fixture_ApplyMapping_SingleTarget_ConstantTransform_1(columnMappings: columnMappings);

    private static global::Nimblesite.Sync.Core.TableMapping Dataset_ApplyMapping_SingleTarget_ExcludesColumns_1() =>
        MappingTestData.Mapping(id: "user-mapping", sourceTable: "User", targetTable: "Customer");

    private static global::Nimblesite.Sync.Core.TableMapping Dataset_ApplyMapping_DisabledMapping_ReturnsSkipped_1() =>
        MappingTestData.Mapping(
            id: "disabled-mapping",
            sourceTable: "User",
            targetTable: "Customer"
        );

    private static global::Nimblesite.Sync.Core.SyncLogEntry Dataset_ApplyMapping_DisabledMapping_ReturnsSkipped_2() =>
        MappingTestData.Entry(table: "User", pk: """{"Id":"u1"}""", payload: """{"Id":"u1"}""");

    private static global::System.Collections.Generic.List<global::Nimblesite.Sync.Core.TargetConfig> Dataset_ApplyMapping_MultiTarget_ProducesMultipleEntries_1() =>
        new List<TargetConfig>
        {
            new(
                "OrderHeader",
                [new ColumnMapping("Id", "OrderId"), new ColumnMapping("Total", "Amount")]
            ),
            new(
                "OrderAudit",
                [
                    new ColumnMapping("Id", "OrderId"),
                    new ColumnMapping(null, "EventType", TransformType.Constant, "created"),
                ]
            ),
        };

    private static global::Nimblesite.Sync.Core.TableMapping Dataset_ApplyMapping_MultiTarget_ProducesMultipleEntries_2() =>
        MappingTestData.Mapping(id: "order-split", sourceTable: "Order", targetTable: null);

    private static global::System.Collections.Generic.List<global::Nimblesite.Sync.Core.TargetConfig> Dataset_ApplyMapping_MultiTarget_EachTargetHasCorrectPayload_1() =>
        new List<TargetConfig>
        {
            new("OrderHeader", [new ColumnMapping("Total", "Amount")]),
            new(
                "OrderAudit",
                [new ColumnMapping(null, "EventType", TransformType.Constant, "created")]
            ),
        };

    private static global::Nimblesite.Sync.Core.TableMapping Dataset_ApplyMapping_MultiTarget_EachTargetHasCorrectPayload_2() =>
        MappingTestData.Mapping(id: "order-split", sourceTable: "Order", targetTable: null);
}
