// Implements [CI-DESLOP-INDEPENDENT-DATA].
namespace Nimblesite.Sync.Tests;

public partial class LqlExpressionEvaluatorTests
{
    private static global::Nimblesite.Sync.Core.ColumnMapping Fixture_MappingEngine_LqlTransform_ConcatNamesForDifferentSchema_1() =>
        new("FirstName", "FullName", TransformType.Lql, null, "concat(FirstName, ' ', LastName)");

    private static global::Nimblesite.Sync.Core.ColumnMapping Fixture_MappingEngine_LqlTransform_NormalizeAndFormat_1() =>
        new(
            "Username",
            "NormalizedUsername",
            TransformType.Lql,
            null,
            "Username |> trim() |> lower()"
        );

    private static global::Nimblesite.Sync.Core.ColumnMapping Fixture_MappingEngine_LqlTransform_FormatDatesForDifferentSystem_1() =>
        new(
            "CreatedAt",
            "CreatedDate",
            TransformType.Lql,
            null,
            "dateFormat(CreatedAt, 'yyyy-MM-dd')"
        );

    private static global::Nimblesite.Sync.Core.ColumnMapping Fixture_MappingEngine_LqlTransform_MultipleTransformsInSameMapping_1() =>
        new("FirstName", "FullName", TransformType.Lql, null, "concat(FirstName, ' ', LastName)");

    private static global::Nimblesite.Sync.Core.ColumnMapping Fixture_E2E_MobileToServerSchemaTransform_1() =>
        new(
            "first_name",
            "FullName",
            TransformType.Lql,
            null,
            "concat(first_name, ' ', last_name)"
        );

    private static global::Nimblesite.Sync.Core.ColumnMapping Fixture_E2E_MobileToServerSchemaTransform_2() =>
        new("email_address", "NormalizedEmail", TransformType.Lql, null, "lower(email_address)");

    private static global::Nimblesite.Sync.Core.TableMapping Fixture_E2E_MobileToServerSchemaTransform_3(
        global::System.Collections.Generic.List<global::Nimblesite.Sync.Core.ColumnMapping> columnMappings
    ) =>
        MappingTestData.Mapping(
            id: "mobile-to-server",
            sourceTable: "MobileUser",
            targetTable: "ServerUser",
            columnMappings: columnMappings
        );

    private static global::Nimblesite.Sync.Core.ColumnMapping Fixture_E2E_LegacyCRMToModernERPTransform_1() =>
        new(
            "ADDR_LINE_1",
            "FullAddress",
            TransformType.Lql,
            null,
            "concat(ADDR_LINE_1, ', ', ADDR_LINE_2)"
        );

    private static global::Nimblesite.Sync.Core.TableMapping Fixture_E2E_LegacyCRMToModernERPTransform_2(
        global::System.Collections.Generic.List<global::Nimblesite.Sync.Core.ColumnMapping> columnMappings
    ) =>
        MappingTestData.Mapping(
            id: "crm-to-erp",
            sourceTable: "LegacyCustomer",
            targetTable: "ModernCustomer",
            columnMappings: columnMappings
        );

    private static global::Nimblesite.Sync.Core.TableMapping Fixture_E2E_OrderToAuditLogTransform_1(
        global::System.Collections.Generic.List<global::Nimblesite.Sync.Core.ColumnMapping> columnMappings
    ) =>
        MappingTestData.Mapping(
            id: "order-to-audit",
            sourceTable: "Order",
            targetTable: "AuditLog",
            columnMappings: columnMappings
        );

    private static global::Nimblesite.Sync.Core.TableMapping Fixture_MapUser_1(
        global::System.Collections.Generic.IReadOnlyList<global::Nimblesite.Sync.Core.ColumnMapping> columnMappings
    ) =>
        MappingTestData.Mapping(
            id: "user-mapping",
            sourceTable: "User",
            targetTable: "Customer",
            columnMappings: columnMappings
        );
}
