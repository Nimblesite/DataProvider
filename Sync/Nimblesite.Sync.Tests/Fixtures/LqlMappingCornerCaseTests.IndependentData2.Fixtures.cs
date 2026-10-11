// Implements [CI-DESLOP-INDEPENDENT-DATA].

namespace Nimblesite.Sync.Tests;

public partial class LqlMappingCornerCaseTests
{
    private static global::Nimblesite.Sync.Core.TableMapping Dataset_MappingEngine_EmptyColumnMappings_PassthroughPayload_1() =>
        MappingTestData.Mapping(id: "passthrough", sourceTable: "Source", targetTable: "Target");

    private static global::System.Collections.Generic.List<global::Nimblesite.Sync.Core.ColumnMapping> Dataset_MappingEngine_LqlWithMissingSourceColumn_FallsBack_1() =>
        new List<ColumnMapping>
        {
            new("NonExistent", "DisplayName", TransformType.Lql, null, "upper(NonExistent)"),
        };

    private static global::Nimblesite.Sync.Core.TableMapping Dataset_MappingEngine_LqlWithMissingSourceColumn_FallsBack_2(
        global::System.Collections.Generic.List<global::Nimblesite.Sync.Core.ColumnMapping> columnMappings
    ) =>
        Fixture_MappingEngine_LqlWithMissingSourceColumn_FallsBack_1(
            columnMappings: columnMappings
        );

    private static global::Nimblesite.Sync.Core.SyncLogEntry Dataset_MappingEngine_LqlWithMissingSourceColumn_FallsBack_3() =>
        MappingTestData.Entry(
            table: "Source",
            pk: """{"Id":"1"}""",
            payload: """{"Id":"1","Name":"Alice"}"""
        );

    private static global::System.Collections.Generic.List<global::Nimblesite.Sync.Core.ColumnMapping> Dataset_MappingEngine_MixedTransformTypes_AllApplied_1() =>
        new List<ColumnMapping>
        {
            new("Name", "Name"), // Direct mapping
            new("Email", "NormalizedEmail", TransformType.Lql, null, "lower(Email)"), // LQL transform
            new(null, "Source", TransformType.Constant, "mobile-app"), // Constant
            new("Status", "StatusCode"), // Direct mapping
        };

    private static global::Nimblesite.Sync.Core.TableMapping Dataset_MappingEngine_MixedTransformTypes_AllApplied_2(
        global::System.Collections.Generic.List<global::Nimblesite.Sync.Core.ColumnMapping> columnMappings
    ) => Fixture_MappingEngine_MixedTransformTypes_AllApplied_1(columnMappings: columnMappings);

    private static global::Nimblesite.Sync.Core.TableMapping Dataset_MappingEngine_ExcludedColumns_RemovedFromPayload_1() =>
        MappingTestData.Mapping(
            id: "with-exclusions",
            sourceTable: "User",
            targetTable: "PublicUser"
        );

    private static global::Nimblesite.Sync.Core.TableMapping Dataset_MappingEngine_PkMapping_RenamesKey_1() =>
        MappingTestData.Mapping(id: "pk-rename", sourceTable: "Source", targetTable: "Target");

    private static global::System.Collections.Generic.List<global::Nimblesite.Sync.Core.TargetConfig> Dataset_MappingEngine_MultiTarget_AllTargetsReceiveTransformedData_1() =>
        new List<TargetConfig>
        {
            new(
                "UserProfile",
                [
                    Fixture_MappingEngine_MultiTarget_AllTargetsReceiveTransformedData_1(),
                    new ColumnMapping("Email", "Email"),
                ]
            ),
            new(
                "UserAudit",
                [
                    new ColumnMapping("Name", "UserName"),
                    new ColumnMapping(null, "EventType", TransformType.Constant, "user_created"),
                ]
            ),
        };

    private static global::Nimblesite.Sync.Core.TableMapping Dataset_MappingEngine_MultiTarget_AllTargetsReceiveTransformedData_2() =>
        MappingTestData.Mapping(id: "multi-target", sourceTable: "User", targetTable: null);

    private static global::System.Collections.Generic.List<global::Nimblesite.Sync.Core.ColumnMapping> Dataset_E2E_FullAddressNormalization_1() =>
        new List<ColumnMapping>
        {
            new("street", "Street", TransformType.Lql, null, "trim(street)"),
            new("city", "City", TransformType.Lql, null, "city |> trim() |> upper()"),
            new("state", "State", TransformType.Lql, null, "upper(state)"),
            new("zip", "PostalCode", TransformType.Lql, null, "left(zip, 5)"),
        };

    private static global::System.Collections.Generic.List<global::Nimblesite.Sync.Core.ColumnMapping> Dataset_E2E_UserDataSanitization_1() =>
        new List<ColumnMapping>
        {
            new("username", "Username", TransformType.Lql, null, "lower(username)"),
            new("email", "Email", TransformType.Lql, null, "lower(email)"),
            new("display_name", "DisplayName", TransformType.Lql, null, "trim(display_name)"),
            // Constant for sync source
            new(null, "DataSource", TransformType.Constant, "legacy_crm"),
        };
}
