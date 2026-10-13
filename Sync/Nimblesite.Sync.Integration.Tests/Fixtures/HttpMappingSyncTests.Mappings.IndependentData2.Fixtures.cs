// Implements [CI-DESLOP-INDEPENDENT-DATA].

namespace Nimblesite.Sync.Integration.Tests;

public partial class HttpMappingSyncTests
{
    private static global::System.Collections.Generic.List<global::Nimblesite.Sync.Core.ColumnMapping> Dataset_Mapping_TransformsColumnsFromUserToCustomer_1() =>
        new List<ColumnMapping>
        {
            new("FullName", "name"),
            new("EmailAddress", "email"),
            new(null, "source", TransformType.Constant, "mobile-app"),
        };

    private static global::System.Collections.Generic.List<global::Nimblesite.Sync.Core.ColumnMapping> Dataset_FullSync_WithMapping_TransformsAndApplies_1() =>
        new List<ColumnMapping>
        {
            new("FullName", "name"),
            new("EmailAddress", "email"),
            new(null, "source", TransformType.Constant, "sync-test"),
        };

    private static global::System.Collections.Generic.List<global::Nimblesite.Sync.Core.TargetConfig> Dataset_MultiTargetMapping_OneSourceToManyTargets_1() =>
        new List<TargetConfig>
        {
            new(
                "OrderHeader",
                [
                    new ColumnMapping("Id", "OrderId"),
                    new ColumnMapping("CustomerId", "CustomerId"),
                    new ColumnMapping("Total", "Amount"),
                ]
            ),
            new(
                "OrderAudit",
                [
                    new ColumnMapping("Id", "OrderId"),
                    new ColumnMapping("CreatedAt", "EventTime"),
                    new ColumnMapping(null, "EventType", TransformType.Constant, "order_created"),
                ]
            ),
        };

    private static global::Nimblesite.Sync.Core.TableMapping Dataset_MultiTargetMapping_OneSourceToManyTargets_2(
        global::System.Collections.Generic.List<global::Nimblesite.Sync.Core.TargetConfig> targets
    ) =>
        new TableMapping(
            Id: "order-split",
            SourceTable: "SalesOrder",
            TargetTable: null,
            Direction: MappingDirection.Push,
            Enabled: true,
            PkMapping: null,
            ColumnMappings: [],
            ExcludedColumns: [],
            Filter: null,
            SyncTracking: new SyncTrackingConfig(),
            IsMultiTarget: true,
            Targets: targets
        );

    private static global::Nimblesite.Sync.Core.TableMapping Dataset_BidirectionalMapping_DifferentConfigsPerDirection_1() =>
        new TableMapping(
            Id: "user-push",
            SourceTable: "User",
            TargetTable: "Customer",
            Direction: MappingDirection.Push,
            Enabled: true,
            PkMapping: new PkMapping("Id", "CustomerId"),
            ColumnMappings: [new("FullName", "Name")],
            ExcludedColumns: [],
            Filter: null,
            SyncTracking: new SyncTrackingConfig()
        );

    private static global::Nimblesite.Sync.Core.TableMapping Dataset_BidirectionalMapping_DifferentConfigsPerDirection_2() =>
        new TableMapping(
            Id: "customer-pull",
            SourceTable: "Customer",
            TargetTable: "User",
            Direction: MappingDirection.Pull,
            Enabled: true,
            PkMapping: new PkMapping("CustomerId", "Id"),
            ColumnMappings: [new("Name", "FullName")],
            ExcludedColumns: [],
            Filter: null,
            SyncTracking: new SyncTrackingConfig()
        );

    private static global::Nimblesite.Sync.Core.SyncMappingConfig Dataset_BidirectionalMapping_DifferentConfigsPerDirection_3(
        global::Nimblesite.Sync.Core.TableMapping pushMapping,
        global::Nimblesite.Sync.Core.TableMapping pullMapping
    ) => new SyncMappingConfig("1.0", UnmappedTableBehavior.Strict, [pushMapping, pullMapping]);
}
