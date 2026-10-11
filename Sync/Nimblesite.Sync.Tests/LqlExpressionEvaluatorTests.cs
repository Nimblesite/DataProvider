using Microsoft.Extensions.Logging.Abstractions;
using static global::Tests.Shared.StringAssertions;
using static Nimblesite.Sync.Tests.MappingTestData;

namespace Nimblesite.Sync.Tests;

// Implements [SYNC-MAPPING-TEST-DATA].

/// <summary>
/// Tests for LqlExpressionEvaluator and LQL transforms in MappingEngine.
/// Proves that LQL can transform data between databases with different schemas.
/// </summary>
public sealed partial class LqlExpressionEvaluatorTests
{
    private readonly NullLogger<LqlExpressionEvaluatorTests> _logger = new();

    #region MappingEngine with LQL Transforms

    [Fact]
    public void MappingEngine_LqlTransform_AppliesUppercase()
    {
        var columnMappings = Dataset_MappingEngine_LqlTransform_AppliesUppercase_1();

        var success = MapUser(
            columnMappings: columnMappings,
            payload: """{"Id":"u1","Name":"alice"}"""
        );
        Assert.Contains("DisplayName", success.Entries[0].MappedPayload);
        Assert.Contains("ALICE", success.Entries[0].MappedPayload);
    }

    [Fact]
    public void MappingEngine_LqlTransform_ConcatNamesForDifferentSchema()
    {
        // Source DB has FirstName, LastName
        // Target DB has FullName
        var columnMappings = Dataset_MappingEngine_LqlTransform_ConcatNamesForDifferentSchema_1();

        var success = MapUser(
            columnMappings: columnMappings,
            payload: """{"Id":"u1","FirstName":"John","LastName":"Doe"}"""
        );
        Assert.Contains("FullName", success.Entries[0].MappedPayload);
        Assert.Contains("John Doe", success.Entries[0].MappedPayload);
    }

    [Fact]
    public void MappingEngine_LqlTransform_ExtractDomainFromEmail()
    {
        // Extract domain from email using substring functions
        // This demonstrates complex data transformation
        var columnMappings = Dataset_MappingEngine_LqlTransform_ExtractDomainFromEmail_1();

        var success = MapUser(
            columnMappings: columnMappings,
            payload: """{"Id":"u1","Email":"user@example.com"}"""
        );
        Assert.Contains("Domain", success.Entries[0].MappedPayload);
        Assert.Contains("example.com", success.Entries[0].MappedPayload);
    }

    [Fact]
    public void MappingEngine_LqlTransform_NormalizeAndFormat()
    {
        // Trim whitespace and convert to lowercase
        var columnMappings = Dataset_MappingEngine_LqlTransform_NormalizeAndFormat_1();

        var success = MapUser(
            columnMappings: columnMappings,
            payload: """{"Id":"u1","Username":"  ALICE  "}"""
        );
        Assert.Contains("NormalizedUsername", success.Entries[0].MappedPayload);
        Assert.Contains("alice", success.Entries[0].MappedPayload);
        Assert.DoesNotContain("ALICE", success.Entries[0].MappedPayload);
        Assert.DoesNotContain("  ", success.Entries[0].MappedPayload);
    }

    [Fact]
    public void MappingEngine_LqlTransform_FormatDatesForDifferentSystem()
    {
        // Transform ISO date to different format
        var columnMappings = Dataset_MappingEngine_LqlTransform_FormatDatesForDifferentSystem_1();

        var success = MapUser(
            columnMappings: columnMappings,
            payload: """{"Id":"u1","CreatedAt":"2024-06-15T10:30:00Z"}"""
        );
        Assert.Contains("CreatedDate", success.Entries[0].MappedPayload);
        Assert.Contains("2024-06-15", success.Entries[0].MappedPayload);
        Assert.DoesNotContain("T10:30:00Z", success.Entries[0].MappedPayload);
    }

    [Fact]
    public void MappingEngine_LqlTransform_MultipleTransformsInSameMapping()
    {
        // Multiple LQL transforms in one mapping
        var columnMappings = Dataset_MappingEngine_LqlTransform_MultipleTransformsInSameMapping_1();

        var success = MapUser(
            columnMappings: columnMappings,
            payload: """{"Id":"u1","FirstName":"John","LastName":"Doe","Email":"John@Example.COM","Username":"johndoe"}"""
        );
        var payload = success.Entries[0].MappedPayload!;
        ContainsAll(actual: payload, expected: ["John Doe", "john@example.com", "JOHNDOE"]);
    }

    [Fact]
    public void MappingEngine_LqlTransform_WithConstantsAndLql()
    {
        // Mix of constant transforms and LQL transforms
        var columnMappings = Dataset_MappingEngine_LqlTransform_WithConstantsAndLql_1();

        var success = MapUser(
            columnMappings: columnMappings,
            payload: """{"Id":"u1","Name":"alice"}"""
        );
        var payload = success.Entries[0].MappedPayload!;
        ContainsAll(actual: payload, expected: ["ALICE", "mobile-app", "Version"]);
    }

    #endregion

    #region E2E Scenario: Different Database Schemas

    /// <summary>
    /// Proves LQL transforms data from a mobile app schema to a backend server schema.
    /// Mobile: { Id, first_name, last_name, email_address, created_date }
    /// Server: { Id, FullName, Email, NormalizedEmail, CreatedAt }
    /// </summary>
    [Fact]
    public void E2E_MobileToServerSchemaTransform()
    {
        var columnMappings = Dataset_E2E_MobileToServerSchemaTransform_1();

        var mapping = Fixture_E2E_MobileToServerSchemaTransform_3(columnMappings: columnMappings);
        var config = MappingTestData.Strict(mappings: [mapping]);

        var mobileEntry = Dataset_E2E_MobileToServerSchemaTransform_2();

        var success = Expected<MappingSuccess>(entry: mobileEntry, config: config, logger: _logger);
        Assert.Single(success.Entries);
        var mapped = success.Entries[0];

        Assert.Equal("ServerUser", mapped.TargetTable);

        var payload = mapped.MappedPayload!;
        ContainsAll(
            actual: payload,
            expected:
            [
                "Jane Smith",
                "Jane.Smith@Company.COM",
                "jane.smith@company.com",
                "2024-01-15",
            ]
        ); // Created date passed through
    }

    /// <summary>
    /// Proves LQL transforms data from a legacy CRM to a modern ERP schema.
    /// Legacy: { CustomerNumber, CUST_NAME, ADDR_LINE_1, ADDR_LINE_2, CITY_NAME, STATE_CD, ZIP_5 }
    /// Modern: { CustomerId, DisplayName, FullAddress, City, State, PostalCode }
    /// </summary>
    [Fact]
    public void E2E_LegacyCRMToModernERPTransform()
    {
        var columnMappings = Dataset_E2E_LegacyCRMToModernERPTransform_1();

        var mapping = Fixture_E2E_LegacyCRMToModernERPTransform_2(columnMappings: columnMappings);
        var config = MappingTestData.Strict(mappings: [mapping]);

        var legacyEntry = Dataset_E2E_LegacyCRMToModernERPTransform_2();

        var success = Expected<MappingSuccess>(entry: legacyEntry, config: config, logger: _logger);
        var payload = success.Entries[0].MappedPayload!;

        Assert.Equal("ModernCustomer", success.Entries[0].TargetTable);
        ContainsAll(actual: payload, expected: ["CustomerId", "C-12345", "ACME CORPORATION"]); // Trimmed
        Assert.DoesNotContain("  ACME", payload); // No leading spaces
        ContainsAll(
            actual: payload,
            expected: ["123 Main St, Suite 100", "Springfield", "IL", "62701"]
        );
    }

    /// <summary>
    /// Proves LQL transforms order data to audit log format.
    /// Order: { OrderId, Total, Status, CustomerId, OrderDate }
    /// AuditLog: { EntityId, EntityType, Action, Timestamp, Details }
    /// </summary>
    [Fact]
    public void E2E_OrderToAuditLogTransform()
    {
        var columnMappings = Dataset_E2E_OrderToAuditLogTransform_1();

        var mapping = Fixture_E2E_OrderToAuditLogTransform_1(columnMappings: columnMappings);
        var config = MappingTestData.Strict(mappings: [mapping]);

        var orderEntry = Dataset_E2E_OrderToAuditLogTransform_2();

        var success = Expected<MappingSuccess>(entry: orderEntry, config: config, logger: _logger);
        var payload = success.Entries[0].MappedPayload!;

        Assert.Equal("AuditLog", success.Entries[0].TargetTable);
        ContainsAll(
            actual: payload,
            expected: ["EntityId", "ORD-001", "Order", "COMPLETED", "Order total: $199.99"]
        ); // Formatted details
    }

    #endregion

    // Implements [SYNC-MAPPING-TRANSFORM-FIXTURE].
    private MappingSuccess MapUser(IReadOnlyList<ColumnMapping> columnMappings, string payload)
    {
        var mapping = Fixture_MapUser_1(columnMappings: columnMappings);
        var entry = MappingTestData.Entry(table: "User", pk: """{"Id":"u1"}""", payload: payload);
        return Dataset_MapUser_1(entry: entry, mapping: mapping);
    }
}
