using Microsoft.Extensions.Logging.Abstractions;

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
        var columnMappings = new List<ColumnMapping>
        {
            new("Name", "DisplayName", TransformType.Lql, null, "upper(Name)"),
        };

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
        var columnMappings = new List<ColumnMapping>
        {
            new(
                "FirstName",
                "FullName",
                TransformType.Lql,
                null,
                "concat(FirstName, ' ', LastName)"
            ),
        };

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
        var columnMappings = new List<ColumnMapping>
        {
            new("Email", "Email"),
            new("Email", "Domain", TransformType.Lql, null, "right(Email, 11)"),
        };

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
        var columnMappings = new List<ColumnMapping>
        {
            new(
                "Username",
                "NormalizedUsername",
                TransformType.Lql,
                null,
                "Username |> trim() |> lower()"
            ),
        };

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
        var columnMappings = new List<ColumnMapping>
        {
            new(
                "CreatedAt",
                "CreatedDate",
                TransformType.Lql,
                null,
                "dateFormat(CreatedAt, 'yyyy-MM-dd')"
            ),
        };

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
        var columnMappings = new List<ColumnMapping>
        {
            new(
                "FirstName",
                "FullName",
                TransformType.Lql,
                null,
                "concat(FirstName, ' ', LastName)"
            ),
            new("Email", "NormalizedEmail", TransformType.Lql, null, "lower(Email)"),
            new("Username", "DisplayName", TransformType.Lql, null, "upper(Username)"),
        };

        var success = MapUser(
            columnMappings: columnMappings,
            payload: """{"Id":"u1","FirstName":"John","LastName":"Doe","Email":"John@Example.COM","Username":"johndoe"}"""
        );
        var payload = success.Entries[0].MappedPayload!;
        Assert.Contains("John Doe", payload);
        Assert.Contains("john@example.com", payload);
        Assert.Contains("JOHNDOE", payload);
    }

    [Fact]
    public void MappingEngine_LqlTransform_WithConstantsAndLql()
    {
        // Mix of constant transforms and LQL transforms
        var columnMappings = new List<ColumnMapping>
        {
            new("Name", "DisplayName", TransformType.Lql, null, "upper(Name)"),
            new(null, "SyncSource", TransformType.Constant, "mobile-app"),
            new(null, "Version", TransformType.Constant, "1.0"),
        };

        var success = MapUser(
            columnMappings: columnMappings,
            payload: """{"Id":"u1","Name":"alice"}"""
        );
        var payload = success.Entries[0].MappedPayload!;
        Assert.Contains("ALICE", payload);
        Assert.Contains("mobile-app", payload);
        Assert.Contains("Version", payload);
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
        var columnMappings = new List<ColumnMapping>
        {
            // Combine first_name + last_name into FullName
            new(
                "first_name",
                "FullName",
                TransformType.Lql,
                null,
                "concat(first_name, ' ', last_name)"
            ),
            // Keep email as-is
            new("email_address", "Email"),
            // Normalize email to lowercase
            new(
                "email_address",
                "NormalizedEmail",
                TransformType.Lql,
                null,
                "lower(email_address)"
            ),
            // Pass through created_date
            new("created_date", "CreatedAt"),
        };

        var mapping = MappingTestData.Mapping(
            id: "mobile-to-server",
            sourceTable: "MobileUser",
            targetTable: "ServerUser",
            columnMappings: columnMappings
        );
        var config = MappingTestData.Strict(mappings: [mapping]);

        var mobileEntry = MappingTestData.Entry(
            table: "MobileUser",
            pk: """{"Id":"u123"}""",
            payload: """{"Id":"u123","first_name":"Jane","last_name":"Smith","email_address":"Jane.Smith@Company.COM","created_date":"2024-01-15"}"""
        );

        var result = MappingEngine.ApplyMapping(
            mobileEntry,
            config,
            MappingDirection.Push,
            _logger
        );

        var success = Assert.IsType<MappingSuccess>(result);
        Assert.Single(success.Entries);
        var mapped = success.Entries[0];

        Assert.Equal("ServerUser", mapped.TargetTable);

        var payload = mapped.MappedPayload!;
        Assert.Contains("Jane Smith", payload); // Concatenated name
        Assert.Contains("Jane.Smith@Company.COM", payload); // Original email preserved
        Assert.Contains("jane.smith@company.com", payload); // Normalized lowercase email
        Assert.Contains("2024-01-15", payload); // Created date passed through
    }

    /// <summary>
    /// Proves LQL transforms data from a legacy CRM to a modern ERP schema.
    /// Legacy: { CustomerNumber, CUST_NAME, ADDR_LINE_1, ADDR_LINE_2, CITY_NAME, STATE_CD, ZIP_5 }
    /// Modern: { CustomerId, DisplayName, FullAddress, City, State, PostalCode }
    /// </summary>
    [Fact]
    public void E2E_LegacyCRMToModernERPTransform()
    {
        var columnMappings = new List<ColumnMapping>
        {
            // Rename CustomerNumber to CustomerId
            new("CustomerNumber", "CustomerId"),
            // Trim and titlecase the name
            new("CUST_NAME", "DisplayName", TransformType.Lql, null, "trim(CUST_NAME)"),
            // Combine address lines
            new(
                "ADDR_LINE_1",
                "FullAddress",
                TransformType.Lql,
                null,
                "concat(ADDR_LINE_1, ', ', ADDR_LINE_2)"
            ),
            // Simple renames
            new("CITY_NAME", "City"),
            new("STATE_CD", "State"),
            new("ZIP_5", "PostalCode"),
        };

        var mapping = MappingTestData.Mapping(
            id: "crm-to-erp",
            sourceTable: "LegacyCustomer",
            targetTable: "ModernCustomer",
            columnMappings: columnMappings
        );
        var config = MappingTestData.Strict(mappings: [mapping]);

        var legacyEntry = MappingTestData.Entry(
            table: "LegacyCustomer",
            pk: """{"CustomerNumber":"C-12345"}""",
            payload: """{"CustomerNumber":"C-12345","CUST_NAME":"  ACME CORPORATION  ","ADDR_LINE_1":"123 Main St","ADDR_LINE_2":"Suite 100","CITY_NAME":"Springfield","STATE_CD":"IL","ZIP_5":"62701"}"""
        );

        var result = MappingEngine.ApplyMapping(
            legacyEntry,
            config,
            MappingDirection.Push,
            _logger
        );

        var success = Assert.IsType<MappingSuccess>(result);
        var payload = success.Entries[0].MappedPayload!;

        Assert.Equal("ModernCustomer", success.Entries[0].TargetTable);
        Assert.Contains("CustomerId", payload);
        Assert.Contains("C-12345", payload);
        Assert.Contains("ACME CORPORATION", payload); // Trimmed
        Assert.DoesNotContain("  ACME", payload); // No leading spaces
        Assert.Contains("123 Main St, Suite 100", payload); // Combined address
        Assert.Contains("Springfield", payload);
        Assert.Contains("IL", payload);
        Assert.Contains("62701", payload);
    }

    /// <summary>
    /// Proves LQL transforms order data to audit log format.
    /// Order: { OrderId, Total, Status, CustomerId, OrderDate }
    /// AuditLog: { EntityId, EntityType, Action, Timestamp, Details }
    /// </summary>
    [Fact]
    public void E2E_OrderToAuditLogTransform()
    {
        var columnMappings = new List<ColumnMapping>
        {
            new("OrderId", "EntityId"),
            new(null, "EntityType", TransformType.Constant, "Order"),
            new("Status", "Action", TransformType.Lql, null, "upper(Status)"),
            new("OrderDate", "Timestamp"),
            new("Total", "Details", TransformType.Lql, null, "concat('Order total: $', Total)"),
        };

        var mapping = MappingTestData.Mapping(
            id: "order-to-audit",
            sourceTable: "Order",
            targetTable: "AuditLog",
            columnMappings: columnMappings
        );
        var config = MappingTestData.Strict(mappings: [mapping]);

        var orderEntry = MappingTestData.Entry(
            table: "Order",
            pk: """{"OrderId":"ORD-001"}""",
            payload: """{"OrderId":"ORD-001","Total":"199.99","Status":"completed","CustomerId":"C-123","OrderDate":"2024-06-15T14:30:00Z"}"""
        );

        var result = MappingEngine.ApplyMapping(orderEntry, config, MappingDirection.Push, _logger);

        var success = Assert.IsType<MappingSuccess>(result);
        var payload = success.Entries[0].MappedPayload!;

        Assert.Equal("AuditLog", success.Entries[0].TargetTable);
        Assert.Contains("EntityId", payload);
        Assert.Contains("ORD-001", payload);
        Assert.Contains("Order", payload); // EntityType constant
        Assert.Contains("COMPLETED", payload); // Uppercased status
        Assert.Contains("Order total: $199.99", payload); // Formatted details
    }

    #endregion

    // Implements [SYNC-MAPPING-TRANSFORM-FIXTURE].
    private MappingSuccess MapUser(IReadOnlyList<ColumnMapping> columnMappings, string payload)
    {
        var mapping = MappingTestData.Mapping(
            id: "user-mapping",
            sourceTable: "User",
            targetTable: "Customer",
            columnMappings: columnMappings
        );
        var entry = MappingTestData.Entry(table: "User", pk: """{"Id":"u1"}""", payload: payload);
        return Assert.IsType<MappingSuccess>(
            MappingEngine.ApplyMapping(
                entry: entry,
                config: MappingTestData.Strict(mappings: [mapping]),
                direction: MappingDirection.Push,
                logger: _logger
            )
        );
    }
}
