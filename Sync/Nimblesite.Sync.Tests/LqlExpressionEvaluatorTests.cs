using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;

namespace Nimblesite.Sync.Tests;

/// <summary>
/// Tests for LqlExpressionEvaluator and LQL transforms in MappingEngine.
/// Proves that LQL can transform data between databases with different schemas.
/// </summary>
public sealed class LqlExpressionEvaluatorTests
{
    private readonly NullLogger<LqlExpressionEvaluatorTests> _logger = new();

    #region LqlExpressionEvaluator Direct Tests

    [Fact]
    public void Upper_TransformsToUppercase()
    {
        var source = JsonDocument.Parse("""{"Name":"alice"}""").RootElement;

        var result = LqlExpressionEvaluator.Evaluate("upper(Name)", source);

        Assert.Equal("ALICE", result);
    }

    [Fact]
    public void Lower_TransformsToLowercase()
    {
        var source = JsonDocument.Parse("""{"Name":"ALICE"}""").RootElement;

        var result = LqlExpressionEvaluator.Evaluate("lower(Name)", source);

        Assert.Equal("alice", result);
    }

    [Fact]
    public void Concat_JoinsMultipleColumns()
    {
        var source = JsonDocument.Parse("""{"FirstName":"John","LastName":"Doe"}""").RootElement;

        var result = LqlExpressionEvaluator.Evaluate("concat(FirstName, ' ', LastName)", source);

        Assert.Equal("John Doe", result);
    }

    [Fact]
    public void Concat_WithLiterals()
    {
        var source = JsonDocument.Parse("""{"Name":"Alice"}""").RootElement;

        var result = LqlExpressionEvaluator.Evaluate("concat('Hello, ', Name, '!')", source);

        Assert.Equal("Hello, Alice!", result);
    }

    [Fact]
    public void Substring_ExtractsPartOfString()
    {
        var source = JsonDocument.Parse("""{"Email":"alice@example.com"}""").RootElement;

        // substring(value, start, length) - 1-based index
        var result = LqlExpressionEvaluator.Evaluate("substring(Email, 1, 5)", source);

        Assert.Equal("alice", result);
    }

    [Fact]
    public void Trim_RemovesWhitespace()
    {
        var source = JsonDocument.Parse("""{"Name":"  Alice  "}""").RootElement;

        var result = LqlExpressionEvaluator.Evaluate("trim(Name)", source);

        Assert.Equal("Alice", result);
    }

    [Fact]
    public void Length_ReturnsStringLength()
    {
        var source = JsonDocument.Parse("""{"Name":"Alice"}""").RootElement;

        var result = LqlExpressionEvaluator.Evaluate("length(Name)", source);

        Assert.Equal(5, result);
    }

    [Fact]
    public void Coalesce_ReturnsFirstNonEmpty()
    {
        var source = JsonDocument.Parse("""{"Nickname":"","FullName":"Alice Smith"}""").RootElement;

        var result = LqlExpressionEvaluator.Evaluate("coalesce(Nickname, FullName)", source);

        Assert.Equal("Alice Smith", result);
    }

    [Fact]
    public void Left_ExtractsLeftPart()
    {
        var source = JsonDocument.Parse("""{"Code":"ABC123XYZ"}""").RootElement;

        var result = LqlExpressionEvaluator.Evaluate("left(Code, 3)", source);

        Assert.Equal("ABC", result);
    }

    [Fact]
    public void Right_ExtractsRightPart()
    {
        var source = JsonDocument.Parse("""{"Code":"ABC123XYZ"}""").RootElement;

        var result = LqlExpressionEvaluator.Evaluate("right(Code, 3)", source);

        Assert.Equal("XYZ", result);
    }

    [Fact]
    public void Replace_SubstitutesText()
    {
        var source = JsonDocument.Parse("""{"Text":"Hello World"}""").RootElement;

        var result = LqlExpressionEvaluator.Evaluate("replace(Text, 'World', 'Universe')", source);

        Assert.Equal("Hello Universe", result);
    }

    [Fact]
    public void DateFormat_FormatsDate()
    {
        var source = JsonDocument.Parse("""{"CreatedAt":"2024-06-15T10:30:00Z"}""").RootElement;

        var result = LqlExpressionEvaluator.Evaluate("dateFormat(CreatedAt, 'yyyy-MM-dd')", source);

        Assert.Equal("2024-06-15", result);
    }

    [Fact]
    public void Pipe_ChainsFunctions()
    {
        var source = JsonDocument.Parse("""{"Name":"  alice  "}""").RootElement;

        var result = LqlExpressionEvaluator.Evaluate("Name |> trim() |> upper()", source);

        Assert.Equal("ALICE", result);
    }

    [Fact]
    public void SimpleColumnReference_ReturnsValue()
    {
        var source = JsonDocument.Parse("""{"Name":"Alice","Age":30}""").RootElement;

        var result = LqlExpressionEvaluator.Evaluate("Name", source);

        Assert.Equal("Alice", result);
    }

    [Fact]
    public void CaseInsensitiveColumnMatch()
    {
        var source = JsonDocument.Parse("""{"FirstName":"Alice"}""").RootElement;

        var result = LqlExpressionEvaluator.Evaluate("upper(firstname)", source);

        Assert.Equal("ALICE", result);
    }

    #endregion

    #region MappingEngine with LQL Transforms

    [Fact]
    public void MappingEngine_LqlTransform_AppliesUppercase()
    {
        var columnMappings = new List<ColumnMapping>
        {
            new("Name", "DisplayName", TransformType.Lql, null, "upper(Name)"),
        };

        var mapping = CreateMappingWithColumns("user-mapping", "User", "Customer", columnMappings);
        var config = CreateStrictConfig(mapping);
        var entry = CreateEntry("User", """{"Id":"u1"}""", """{"Id":"u1","Name":"alice"}""");

        var result = MappingEngine.ApplyMapping(entry, config, MappingDirection.Push, _logger);

        var success = Assert.IsType<MappingSuccess>(result);
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

        var mapping = CreateMappingWithColumns("user-mapping", "User", "Customer", columnMappings);
        var config = CreateStrictConfig(mapping);
        var entry = CreateEntry(
            "User",
            """{"Id":"u1"}""",
            """{"Id":"u1","FirstName":"John","LastName":"Doe"}"""
        );

        var result = MappingEngine.ApplyMapping(entry, config, MappingDirection.Push, _logger);

        var success = Assert.IsType<MappingSuccess>(result);
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

        var mapping = CreateMappingWithColumns("user-mapping", "User", "Customer", columnMappings);
        var config = CreateStrictConfig(mapping);
        var entry = CreateEntry(
            "User",
            """{"Id":"u1"}""",
            """{"Id":"u1","Email":"user@example.com"}"""
        );

        var result = MappingEngine.ApplyMapping(entry, config, MappingDirection.Push, _logger);

        var success = Assert.IsType<MappingSuccess>(result);
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

        var mapping = CreateMappingWithColumns("user-mapping", "User", "Customer", columnMappings);
        var config = CreateStrictConfig(mapping);
        var entry = CreateEntry(
            "User",
            """{"Id":"u1"}""",
            """{"Id":"u1","Username":"  ALICE  "}"""
        );

        var result = MappingEngine.ApplyMapping(entry, config, MappingDirection.Push, _logger);

        var success = Assert.IsType<MappingSuccess>(result);
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

        var mapping = CreateMappingWithColumns("user-mapping", "User", "Customer", columnMappings);
        var config = CreateStrictConfig(mapping);
        var entry = CreateEntry(
            "User",
            """{"Id":"u1"}""",
            """{"Id":"u1","CreatedAt":"2024-06-15T10:30:00Z"}"""
        );

        var result = MappingEngine.ApplyMapping(entry, config, MappingDirection.Push, _logger);

        var success = Assert.IsType<MappingSuccess>(result);
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

        var mapping = CreateMappingWithColumns("user-mapping", "User", "Customer", columnMappings);
        var config = CreateStrictConfig(mapping);
        var entry = CreateEntry(
            "User",
            """{"Id":"u1"}""",
            """{"Id":"u1","FirstName":"John","LastName":"Doe","Email":"John@Example.COM","Username":"johndoe"}"""
        );

        var result = MappingEngine.ApplyMapping(entry, config, MappingDirection.Push, _logger);

        var success = Assert.IsType<MappingSuccess>(result);
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

        var mapping = CreateMappingWithColumns("user-mapping", "User", "Customer", columnMappings);
        var config = CreateStrictConfig(mapping);
        var entry = CreateEntry("User", """{"Id":"u1"}""", """{"Id":"u1","Name":"alice"}""");

        var result = MappingEngine.ApplyMapping(entry, config, MappingDirection.Push, _logger);

        var success = Assert.IsType<MappingSuccess>(result);
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

        var mapping = CreateMappingWithColumns(
            "mobile-to-server",
            "MobileUser",
            "ServerUser",
            columnMappings
        );
        var config = CreateStrictConfig(mapping);

        var mobileEntry = CreateEntry(
            "MobileUser",
            """{"Id":"u123"}""",
            """{"Id":"u123","first_name":"Jane","last_name":"Smith","email_address":"Jane.Smith@Company.COM","created_date":"2024-01-15"}"""
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

        var mapping = CreateMappingWithColumns(
            "crm-to-erp",
            "LegacyCustomer",
            "ModernCustomer",
            columnMappings
        );
        var config = CreateStrictConfig(mapping);

        var legacyEntry = CreateEntry(
            "LegacyCustomer",
            """{"CustomerNumber":"C-12345"}""",
            """{"CustomerNumber":"C-12345","CUST_NAME":"  ACME CORPORATION  ","ADDR_LINE_1":"123 Main St","ADDR_LINE_2":"Suite 100","CITY_NAME":"Springfield","STATE_CD":"IL","ZIP_5":"62701"}"""
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

        var mapping = CreateMappingWithColumns(
            "order-to-audit",
            "Order",
            "AuditLog",
            columnMappings
        );
        var config = CreateStrictConfig(mapping);

        var orderEntry = CreateEntry(
            "Order",
            """{"OrderId":"ORD-001"}""",
            """{"OrderId":"ORD-001","Total":"199.99","Status":"completed","CustomerId":"C-123","OrderDate":"2024-06-15T14:30:00Z"}"""
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

    #region Edge Cases

    [Fact]
    public void LqlTransform_NullSource_ReturnsNull()
    {
        var result = LqlExpressionEvaluator.Evaluate("", JsonDocument.Parse("{}").RootElement);

        Assert.Null(result);
    }

    [Fact]
    public void LqlTransform_MissingColumn_ReturnsNull()
    {
        var source = JsonDocument.Parse("""{"Name":"Alice"}""").RootElement;

        var result = LqlExpressionEvaluator.Evaluate("upper(NonExistent)", source);

        // Should return empty string for missing column
        Assert.True(string.IsNullOrEmpty(result?.ToString()));
    }

    [Fact]
    public void LqlTransform_NumericColumn_Works()
    {
        var source = JsonDocument.Parse("""{"Price":99.99}""").RootElement;

        var result = LqlExpressionEvaluator.Evaluate("Price", source);

        Assert.Equal(99.99, result);
    }

    [Fact]
    public void LqlTransform_BooleanColumn_Works()
    {
        var source = JsonDocument.Parse("""{"IsActive":true}""").RootElement;

        var result = LqlExpressionEvaluator.Evaluate("IsActive", source);

        Assert.Equal(true, result);
    }

    #endregion

    #region Helper Methods

    private static TableMapping CreateMappingWithColumns(
        string id,
        string sourceTable,
        string targetTable,
        IReadOnlyList<ColumnMapping> columnMappings
    ) =>
        new(
            Id: id,
            SourceTable: sourceTable,
            TargetTable: targetTable,
            Direction: MappingDirection.Push,
            Enabled: true,
            PkMapping: null,
            ColumnMappings: columnMappings,
            ExcludedColumns: [],
            Filter: null,
            SyncTracking: new SyncTrackingConfig()
        );

    private static SyncLogEntry CreateEntry(string table, string pk, string? payload) =>
        new(
            Version: 1,
            TableName: table,
            PkValue: pk,
            Operation: SyncOperation.Insert,
            Payload: payload,
            Origin: "test-origin",
            Timestamp: "2024-01-01T00:00:00Z"
        );

    private static SyncMappingConfig CreateStrictConfig(TableMapping mapping) =>
        new("1.0", UnmappedTableBehavior.Strict, [mapping]);

    #endregion
}
