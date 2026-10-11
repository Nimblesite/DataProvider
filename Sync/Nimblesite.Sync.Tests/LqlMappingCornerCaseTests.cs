using Microsoft.Extensions.Logging.Abstractions;
using static global::Tests.Shared.StringAssertions;
using static Nimblesite.Sync.Tests.MappingTestData;

namespace Nimblesite.Sync.Tests;

// Implements [SYNC-MAPPING-TEST-DATA].

/// <summary>
/// Corner case and edge case tests for LQL mapping.
/// Tests null handling, empty strings, special characters, Unicode, nested data,
/// and unusual transformation scenarios.
/// </summary>
public sealed partial class LqlMappingCornerCaseTests
{
    private readonly NullLogger<LqlMappingCornerCaseTests> _logger = new();

    #region MappingEngine Corner Cases

    [Fact]
    public void MappingEngine_NullPayload_ReturnsNullMappedPayload()
    {
        var mapping = Fixture_MappingEngine_NullPayload_ReturnsNullMappedPayload_1();
        var config = MappingTestData.Strict(mappings: [mapping]);

        var entry = Fixture_MappingEngine_NullPayload_ReturnsNullMappedPayload_2();

        var success = Expected<MappingSuccess>(entry: entry, config: config, logger: _logger);
        Assert.Null(success.Entries[0].MappedPayload);
    }

    [Fact]
    public void MappingEngine_EmptyColumnMappings_PassthroughPayload()
    {
        var mapping = Dataset_MappingEngine_EmptyColumnMappings_PassthroughPayload_1();
        var success = ApplyMappingToEntry(
            input0: mapping,
            value0: "Source",
            value1: """{"Id":"1"}""",
            value2: """{"Id":"1","Name":"Test","Value":123}"""
        );
        var payload = success.Entries[0].MappedPayload!;
        ContainsAll(actual: payload, expected: ["Name", "Test", "Value"]);
    }

    [Fact]
    public void MappingEngine_LqlWithMissingSourceColumn_FallsBack()
    {
        var columnMappings = Dataset_MappingEngine_LqlWithMissingSourceColumn_FallsBack_1();

        var mapping = Dataset_MappingEngine_LqlWithMissingSourceColumn_FallsBack_2(
            columnMappings: columnMappings
        );
        var config = MappingTestData.Strict(mappings: [mapping]);

        var entry = Dataset_MappingEngine_LqlWithMissingSourceColumn_FallsBack_3();

        // Should not throw, should handle gracefully
        var success = Expected<MappingSuccess>(entry: entry, config: config, logger: _logger);
        Assert.NotNull(success.Entries[0].MappedPayload);
    }

    [Fact]
    public void MappingEngine_MixedTransformTypes_AllApplied()
    {
        var columnMappings = Dataset_MappingEngine_MixedTransformTypes_AllApplied_1();

        var mapping = Dataset_MappingEngine_MixedTransformTypes_AllApplied_2(
            columnMappings: columnMappings
        );
        var success = ApplyMappingToEntry(
            input0: mapping,
            value0: "Source",
            value1: """{"Id":"1"}""",
            value2: """{"Id":"1","Name":"Alice","Email":"ALICE@TEST.COM","Status":"active"}"""
        );
        var payload = success.Entries[0].MappedPayload!;
        ContainsAll(actual: payload, expected: ["Alice", "alice@test.com", "mobile-app", "active"]); // Direct
    }

    [Fact]
    public void MappingEngine_ExcludedColumns_RemovedFromPayload()
    {
        var mapping = Dataset_MappingEngine_ExcludedColumns_RemovedFromPayload_1() with
        {
            ExcludedColumns = ["Password", "Salt", "SecurityToken", "InternalNotes"],
        };
        var success = ApplyMappingToEntry(
            input0: mapping,
            value0: "User",
            value1: """{"Id":"1"}""",
            value2: """{"Id":"1","Name":"Alice","Email":"a@test.com","Password":"secret123","Salt":"xyz","SecurityToken":"abc","InternalNotes":"admin notes","PublicBio":"Hello!"}"""
        );
        var payload = success.Entries[0].MappedPayload!;

        ContainsAll(actual: payload, expected: ["Name", "Email", "PublicBio"]);
        Assert.DoesNotContain("Password", payload);
        Assert.DoesNotContain("Salt", payload);
        Assert.DoesNotContain("SecurityToken", payload);
        Assert.DoesNotContain("InternalNotes", payload);
    }

    [Fact]
    public void MappingEngine_PkMapping_RenamesKey()
    {
        var mapping = Dataset_MappingEngine_PkMapping_RenamesKey_1() with
        {
            PkMapping = new PkMapping("UserId", "CustomerId"),
        };
        var success = ApplyMappingToEntry(
            input0: mapping,
            value0: "Source",
            value1: """{"UserId":"user-123"}""",
            value2: """{"Name":"Test"}"""
        );
        Assert.Contains("CustomerId", success.Entries[0].TargetPkValue);
        Assert.Contains("user-123", success.Entries[0].TargetPkValue);
        Assert.DoesNotContain("UserId", success.Entries[0].TargetPkValue);
    }

    [Fact]
    public void MappingEngine_MultiTarget_AllTargetsReceiveTransformedData()
    {
        var targets = Dataset_MappingEngine_MultiTarget_AllTargetsReceiveTransformedData_1();

        var mapping = Dataset_MappingEngine_MultiTarget_AllTargetsReceiveTransformedData_2() with
        {
            IsMultiTarget = true,
            Targets = targets,
        };
        var success = ApplyMappingToEntry(
            input0: mapping,
            value0: "User",
            value1: """{"Id":"1"}""",
            value2: """{"Id":"1","Name":"alice","Email":"alice@test.com"}"""
        );
        Assert.Equal(2, success.Entries.Count);

        var profile = success.Entries.First(e => e.TargetTable == "UserProfile");
        Assert.Contains("ALICE", profile.MappedPayload); // Uppercased

        var audit = success.Entries.First(e => e.TargetTable == "UserAudit");
        Assert.Contains("alice", audit.MappedPayload); // Original case
        Assert.Contains("user_created", audit.MappedPayload); // Constant
    }

    #endregion

    #region Complex Real-World Scenarios

    [Fact]
    public void E2E_FullAddressNormalization()
    {
        // Simulate normalizing messy address data
        var columnMappings = Dataset_E2E_FullAddressNormalization_1();

        var mapping = Fixture_E2E_FullAddressNormalization_1(columnMappings: columnMappings);
        var success = ApplyMappingToEntry(
            input0: mapping,
            value0: "RawAddress",
            value1: """{"Id":"1"}""",
            value2: """{"Id":"1","street":"  123 Main St  ","city":"  springfield  ","state":"il","zip":"62701-1234"}"""
        );
        var payload = success.Entries[0].MappedPayload!;

        ContainsAll(actual: payload, expected: ["123 Main St", "SPRINGFIELD", "IL", "62701"]); // First 5 chars of zip
        Assert.DoesNotContain("-1234", payload); // ZIP+4 removed
    }

    [Fact]
    public void E2E_UserDataSanitization()
    {
        // Remove sensitive fields, normalize remaining
        var columnMappings = Dataset_E2E_UserDataSanitization_1();

        var mapping = Fixture_E2E_UserDataSanitization_1(columnMappings: columnMappings) with
        {
            PkMapping = new PkMapping("legacy_id", "Id"),
            ExcludedColumns = ["password_hash", "ssn", "credit_card"],
        };
        var success = ApplyMappingToEntry(
            input0: mapping,
            value0: "LegacyUser",
            value1: """{"legacy_id":"USR-001"}""",
            value2: """{"legacy_id":"USR-001","username":"JohnDoe123","email":"John.Doe@Example.COM","display_name":"  John Doe  ","password_hash":"abc123","ssn":"123-45-6789","credit_card":"4111111111111111"}"""
        );
        var payload = success.Entries[0].MappedPayload!;

        // PK renamed
        Assert.Contains("Id", success.Entries[0].TargetPkValue);
        Assert.Contains("USR-001", success.Entries[0].TargetPkValue);

        // Normalized fields
        ContainsAll(actual: payload, expected: ["johndoe123", "john.doe@example.com", "John Doe"]); // Trimmed display name

        // Sensitive fields excluded
        Assert.DoesNotContain("password", payload);
        Assert.DoesNotContain("ssn", payload);
        Assert.DoesNotContain("credit_card", payload);
        Assert.DoesNotContain("4111", payload);
    }

    #endregion

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private global::Nimblesite.Sync.Core.MappingSuccess ApplyMappingToEntry(
        global::Nimblesite.Sync.Core.TableMapping input0,
        string value0,
        string value1,
        string value2
    )
    {
        var config = MappingTestData.Strict(mappings: [input0]);

        var entry = MappingTestData.Entry(table: value0, pk: value1, payload: value2);

        var success = Expected<MappingSuccess>(entry: entry, config: config, logger: _logger);

        return success;
    }
}
