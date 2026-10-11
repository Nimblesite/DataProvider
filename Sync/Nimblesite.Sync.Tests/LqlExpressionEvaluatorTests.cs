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

    // Implements [CI-TEST-SHARED-CASE-MATRIX].
    [Theory]
    [MemberData(nameof(IndependentInstanceScenarioData1))]
    public void MappingEngine_LqlTransform_AppliesUppercase_InstanceScenarioCases(
        string scenarioName,
        global::System.Func<global::System.Collections.Generic.List<global::Nimblesite.Sync.Core.ColumnMapping>> scenario1,
        System.String scenario2,
        System.String scenario3,
        System.String scenario4
    )
    {
        Assert.False(string.IsNullOrEmpty(scenarioName));
        var success = VerifySharedContinuation1(
            input0: scenario1,
            value0: scenario2,
            value1: scenario3
        );
        Assert.Contains(scenario4, success.Entries[0].MappedPayload);
    }

    [Fact]
    public void MappingEngine_LqlTransform_NormalizeAndFormat()
    {
        // Trim whitespace and convert to lowercase
        var success = VerifySharedContinuation1(
            input0: Dataset_MappingEngine_LqlTransform_NormalizeAndFormat_1,
            value0: """{"Id":"u1","Username":"  ALICE  "}""",
            value1: "NormalizedUsername"
        );
        Assert.Contains("alice", success.Entries[0].MappedPayload);
        Assert.DoesNotContain("ALICE", success.Entries[0].MappedPayload);
        Assert.DoesNotContain("  ", success.Entries[0].MappedPayload);
    }

    [Fact]
    public void MappingEngine_LqlTransform_FormatDatesForDifferentSystem()
    {
        // Transform ISO date to different format
        var success = VerifySharedContinuation1(
            input0: Dataset_MappingEngine_LqlTransform_FormatDatesForDifferentSystem_1,
            value0: """{"Id":"u1","CreatedAt":"2024-06-15T10:30:00Z"}""",
            value1: "CreatedDate"
        );
        Assert.Contains("2024-06-15", success.Entries[0].MappedPayload);
        Assert.DoesNotContain("T10:30:00Z", success.Entries[0].MappedPayload);
    }

    [Fact]
    public void MappingEngine_LqlTransform_MultipleTransformsInSameMapping()
    {
        // Multiple LQL transforms in one mapping
        VerifyLqlTransforms(
            mapping: Dataset_MappingEngine_LqlTransform_MultipleTransformsInSameMapping_1,
            payload: """{"Id":"u1","FirstName":"John","LastName":"Doe","Email":"John@Example.COM","Username":"johndoe"}""",
            first: "John Doe",
            second: "john@example.com",
            third: "JOHNDOE"
        );
    }

    [Fact]
    public void MappingEngine_LqlTransform_WithConstantsAndLql()
    {
        // Mix of constant transforms and LQL transforms
        VerifyLqlTransforms(
            mapping: Dataset_MappingEngine_LqlTransform_WithConstantsAndLql_1,
            payload: """{"Id":"u1","Name":"alice"}""",
            first: "ALICE",
            second: "mobile-app",
            third: "Version"
        );
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
        var success = VerifySharedChecks5(
            input0: Dataset_E2E_MobileToServerSchemaTransform_1,
            input1: Fixture_E2E_MobileToServerSchemaTransform_3,
            input2: Dataset_E2E_MobileToServerSchemaTransform_2,
            input3: _logger
        );
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
        var payload = VerifyReusableWorkflow8(
            input0: VerifySharedChecks5,
            input1: Dataset_E2E_LegacyCRMToModernERPTransform_1,
            input2: Fixture_E2E_LegacyCRMToModernERPTransform_2,
            input3: Dataset_E2E_LegacyCRMToModernERPTransform_2,
            input4: _logger,
            value1: "ModernCustomer"
        );
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
        var payload = VerifyReusableWorkflow8(
            input0: VerifySharedChecks5,
            input1: Dataset_E2E_OrderToAuditLogTransform_1,
            input2: Fixture_E2E_OrderToAuditLogTransform_1,
            input3: Dataset_E2E_OrderToAuditLogTransform_2,
            input4: _logger,
            value1: "AuditLog"
        );
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

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private static MappingSuccess VerifySharedChecks5(
        Func<List<ColumnMapping>> input0,
        Func<List<ColumnMapping>, TableMapping> input1,
        Func<SyncLogEntry> input2,
        NullLogger<LqlExpressionEvaluatorTests> input3
    )
    {
        var columnMappings = input0();

        var mapping = input1(columnMappings);
        var config = MappingTestData.Strict(mappings: [mapping]);

        var mobileEntry = input2();

        var success = Expected<MappingSuccess>(entry: mobileEntry, config: config, logger: input3);

        return success;
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    internal MappingSuccess VerifySharedStep1(
        List<ColumnMapping> input0,
        string value0,
        string value1
    )
    {
        var success = MapUser(columnMappings: input0, payload: value0);
        Assert.Contains(value1, success.Entries[0].MappedPayload);

        return success;
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private MappingSuccess VerifySharedContinuation1(
        Func<List<ColumnMapping>> input0,
        string value0,
        string value1
    )
    {
        var columnMappings = input0();

        var success = VerifySharedStep1(input0: columnMappings, value0: value0, value1: value1);

        return success;
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    internal static string VerifyReusableWorkflow8(
        Func<
            Func<List<ColumnMapping>>,
            Func<List<ColumnMapping>, TableMapping>,
            Func<SyncLogEntry>,
            NullLogger<LqlExpressionEvaluatorTests>,
            MappingSuccess
        > input0,
        Func<List<ColumnMapping>> input1,
        Func<List<ColumnMapping>, TableMapping> input2,
        Func<SyncLogEntry> input3,
        NullLogger<LqlExpressionEvaluatorTests> input4,
        string value1
    )
    {
        var success = input0(arg1: input1, arg2: input2, arg3: input3, arg4: input4);
        var payload = success.Entries[0].MappedPayload!;

        Assert.Equal(value1, success.Entries[0].TargetTable);

        return payload;
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private void VerifyLqlTransforms(
        Func<List<ColumnMapping>> mapping,
        string payload,
        string first,
        string second,
        string third
    ) =>
        VerifyReusableWorkflow11(
            input0: mapping,
            input1: MapUser,
            input2: ContainsAll,
            value0: payload,
            value2: first,
            value3: second,
            value4: third
        );

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private void VerifyReusableWorkflow11(
        Func<List<ColumnMapping>> input0,
        Func<IReadOnlyList<ColumnMapping>, string, MappingSuccess> input1,
        Action<string?, System.Collections.Immutable.ImmutableArray<string>> input2,
        string value0,
        string value2,
        string value3,
        string value4
    )
    { // Multiple LQL transforms in one mapping
        var columnMappings = input0();

        var success = input1(arg1: columnMappings, arg2: value0);
        var payload = success.Entries[0].MappedPayload!;
        input2(arg1: payload, arg2: [value2, value3, value4]);
    }
}
