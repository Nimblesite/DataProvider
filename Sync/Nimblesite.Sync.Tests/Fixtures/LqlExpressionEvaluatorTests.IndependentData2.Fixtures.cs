// Implements [CI-DESLOP-INDEPENDENT-DATA].

using static Nimblesite.Sync.Tests.MappingTestData;

namespace Nimblesite.Sync.Tests;

public partial class LqlExpressionEvaluatorTests
{
    private static global::System.Collections.Generic.List<global::Nimblesite.Sync.Core.ColumnMapping> Dataset_MappingEngine_LqlTransform_AppliesUppercase_1() =>
        new List<ColumnMapping>
        {
            new("Name", "DisplayName", TransformType.Lql, null, "upper(Name)"),
        };

    private static global::System.Collections.Generic.List<global::Nimblesite.Sync.Core.ColumnMapping> Dataset_MappingEngine_LqlTransform_ConcatNamesForDifferentSchema_1() =>
        new List<ColumnMapping>
        {
            Fixture_MappingEngine_LqlTransform_ConcatNamesForDifferentSchema_1(),
        };

    private static global::System.Collections.Generic.List<global::Nimblesite.Sync.Core.ColumnMapping> Dataset_MappingEngine_LqlTransform_ExtractDomainFromEmail_1() =>
        new List<ColumnMapping>
        {
            new("Email", "Email"),
            new("Email", "Domain", TransformType.Lql, null, "right(Email, 11)"),
        };

    private static global::System.Collections.Generic.List<global::Nimblesite.Sync.Core.ColumnMapping> Dataset_MappingEngine_LqlTransform_NormalizeAndFormat_1() =>
        new List<ColumnMapping> { Fixture_MappingEngine_LqlTransform_NormalizeAndFormat_1() };

    private static global::System.Collections.Generic.List<global::Nimblesite.Sync.Core.ColumnMapping> Dataset_MappingEngine_LqlTransform_FormatDatesForDifferentSystem_1() =>
        new List<ColumnMapping>
        {
            Fixture_MappingEngine_LqlTransform_FormatDatesForDifferentSystem_1(),
        };

    private static global::System.Collections.Generic.List<global::Nimblesite.Sync.Core.ColumnMapping> Dataset_MappingEngine_LqlTransform_MultipleTransformsInSameMapping_1() =>
        new List<ColumnMapping>
        {
            Fixture_MappingEngine_LqlTransform_MultipleTransformsInSameMapping_1(),
            new("Email", "NormalizedEmail", TransformType.Lql, null, "lower(Email)"),
            new("Username", "DisplayName", TransformType.Lql, null, "upper(Username)"),
        };

    private static global::System.Collections.Generic.List<global::Nimblesite.Sync.Core.ColumnMapping> Dataset_MappingEngine_LqlTransform_WithConstantsAndLql_1() =>
        new List<ColumnMapping>
        {
            new("Name", "DisplayName", TransformType.Lql, null, "upper(Name)"),
            new(null, "SyncSource", TransformType.Constant, "mobile-app"),
            new(null, "Version", TransformType.Constant, "1.0"),
        };

    private static global::System.Collections.Generic.List<global::Nimblesite.Sync.Core.ColumnMapping> Dataset_E2E_MobileToServerSchemaTransform_1() =>
        new List<ColumnMapping>
        {
            // Combine first_name + last_name into FullName
            Fixture_E2E_MobileToServerSchemaTransform_1(),
            // Keep email as-is
            new("email_address", "Email"),
            // Normalize email to lowercase
            Fixture_E2E_MobileToServerSchemaTransform_2(),
            // Pass through created_date
            new("created_date", "CreatedAt"),
        };

    private static global::Nimblesite.Sync.Core.SyncLogEntry Dataset_E2E_MobileToServerSchemaTransform_2() =>
        MappingTestData.Entry(
            table: "MobileUser",
            pk: """{"Id":"u123"}""",
            payload: """{"Id":"u123","first_name":"Jane","last_name":"Smith","email_address":"Jane.Smith@Company.COM","created_date":"2024-01-15"}"""
        );

    private static global::System.Collections.Generic.List<global::Nimblesite.Sync.Core.ColumnMapping> Dataset_E2E_LegacyCRMToModernERPTransform_1() =>
        new List<ColumnMapping>
        {
            // Rename CustomerNumber to CustomerId
            new("CustomerNumber", "CustomerId"),
            // Trim and titlecase the name
            new("CUST_NAME", "DisplayName", TransformType.Lql, null, "trim(CUST_NAME)"),
            // Combine address lines
            Fixture_E2E_LegacyCRMToModernERPTransform_1(),
            // Simple renames
            new("CITY_NAME", "City"),
            new("STATE_CD", "State"),
            new("ZIP_5", "PostalCode"),
        };

    private static global::Nimblesite.Sync.Core.SyncLogEntry Dataset_E2E_LegacyCRMToModernERPTransform_2() =>
        MappingTestData.Entry(
            table: "LegacyCustomer",
            pk: """{"CustomerNumber":"C-12345"}""",
            payload: """{"CustomerNumber":"C-12345","CUST_NAME":"  ACME CORPORATION  ","ADDR_LINE_1":"123 Main St","ADDR_LINE_2":"Suite 100","CITY_NAME":"Springfield","STATE_CD":"IL","ZIP_5":"62701"}"""
        );

    private static global::System.Collections.Generic.List<global::Nimblesite.Sync.Core.ColumnMapping> Dataset_E2E_OrderToAuditLogTransform_1() =>
        new List<ColumnMapping>
        {
            new("OrderId", "EntityId"),
            new(null, "EntityType", TransformType.Constant, "Order"),
            new("Status", "Action", TransformType.Lql, null, "upper(Status)"),
            new("OrderDate", "Timestamp"),
            new("Total", "Details", TransformType.Lql, null, "concat('Order total: $', Total)"),
        };

    private static global::Nimblesite.Sync.Core.SyncLogEntry Dataset_E2E_OrderToAuditLogTransform_2() =>
        MappingTestData.Entry(
            table: "Order",
            pk: """{"OrderId":"ORD-001"}""",
            payload: """{"OrderId":"ORD-001","Total":"199.99","Status":"completed","CustomerId":"C-123","OrderDate":"2024-06-15T14:30:00Z"}"""
        );

    private global::Nimblesite.Sync.Core.MappingSuccess Dataset_MapUser_1(
        global::Nimblesite.Sync.Core.SyncLogEntry entry,
        global::Nimblesite.Sync.Core.TableMapping mapping
    ) =>
        Expected<MappingSuccess>(
            entry: entry,
            config: MappingTestData.Strict(mappings: [mapping]),
            logger: _logger
        );
}
