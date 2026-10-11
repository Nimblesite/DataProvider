using static global::Tests.Shared.StringAssertions;
using static Nimblesite.Sync.Tests.MappingTestData;

namespace Nimblesite.Sync.Tests;

// Implements [SYNC-MAPPING-TEST-DATA].

/// <summary>
/// Tests for MappingEngine - data transformation during sync.
/// Covers spec Section 7 - Data Mapping.
/// </summary>
public sealed partial class MappingEngineTests
{
    private readonly NullLogger<MappingEngineTests> _logger = new();

    // Implements [CI-TEST-SHARED-CASE-MATRIX].
    [Theory]
    [MemberData(nameof(FindMapping_ExactMatch_ReturnsMapping_ContextCasesData))]
    public void FindMapping_ExactMatch_ReturnsMapping_ContextCases(
        string caseName,
        Func<TableMapping> input1,
        string value1
    )
    {
        Assert.False(condition: string.IsNullOrEmpty(value: caseName));
        ExerciseProjectionCaseMatrix4(input0: VerifySharedChecks6, input1: input1, value1: value1);
    }

    [Fact]
    public void FindMapping_BothDirection_MatchesPull()
    {
        var found = VerifySharedChecks6(
            input0: Fixture_FindMapping_BothDirection_MatchesPull_1,
            value1: MappingDirection.Pull
        );

        Assert.NotNull(found);
    }

    // Implements [CI-TEST-SHARED-CASE-MATRIX].
    [Theory]
    [MemberData(nameof(FindMapping_WrongDirection_ReturnsNull_ContextCasesData))]
    public void FindMapping_WrongDirection_ReturnsNull_ContextCases(
        string caseName,
        Func<TableMapping> input1,
        global::Nimblesite.Sync.Core.MappingDirection value0
    )
    {
        Assert.False(condition: string.IsNullOrEmpty(value: caseName));
        ExerciseProjectionCaseMatrix5(input0: VerifySharedChecks6, input1: input1, value0: value0);
    }

    [Fact]
    public void ApplyMapping_PassthroughMode_NoMapping_ReturnsIdentity()
    {
        var config = SyncMappingConfig.Passthrough;
        var success = ApplyConfiguredMappingToEntry(
            input0: config,
            value0: "Person",
            value1: """{"Id":"p1"}""",
            value2: """{"Id":"p1","Name":"Alice"}"""
        );
        Assert.Single(success.Entries);
        Assert.Equal("Person", success.Entries[0].TargetTable);
        Assert.Equal("""{"Id":"p1"}""", success.Entries[0].TargetPkValue);
    }

    [Fact]
    public void ApplyMapping_StrictMode_NoMapping_ReturnsSkipped()
    {
        var config = SyncMappingConfig.Empty;
        var entry = Dataset_ApplyMapping_StrictMode_NoMapping_ReturnsSkipped_1();

        var skipped = Expected<MappingSkipped>(entry: entry, config: config, logger: _logger);
        Assert.Contains("Person", skipped.Reason);
    }

    [Fact]
    public void ApplyMapping_SingleTarget_RenamesTable()
    {
        var mapping = Fixture_ApplyMapping_SingleTarget_RenamesTable_1();
        var success = VerifySharedChecks1(
            input0: mapping,
            value0: "User",
            value1: """{"Id":"u1"}""",
            value2: """{"Id":"u1","Name":"Alice"}"""
        );
        Assert.Single(success.Entries);
        Assert.Equal("Customer", success.Entries[0].TargetTable);
    }

    [Fact]
    public void ApplyMapping_SingleTarget_MapsPrimaryKey()
    {
        var mapping = Dataset_ApplyMapping_SingleTarget_MapsPrimaryKey_1() with
        {
            PkMapping = new PkMapping("Id", "CustomerId"),
        };
        var success = VerifySharedChecks1(
            input0: mapping,
            value0: "User",
            value1: """{"Id":"u1"}""",
            value2: """{"Id":"u1","Name":"Alice"}"""
        );
        Assert.Contains("CustomerId", success.Entries[0].TargetPkValue);
        Assert.Contains("u1", success.Entries[0].TargetPkValue);
    }

    [Fact]
    public void ApplyMapping_SingleTarget_MapsColumns()
    {
        var success = VerifySharedStep2(
            input0: Dataset_ApplyMapping_SingleTarget_MapsColumns_1,
            input1: Dataset_ApplyMapping_SingleTarget_MapsColumns_2,
            value2: """{"Id":"u1","FullName":"Alice","EmailAddress":"alice@test.com"}""",
            value3: "Name"
        );
        Assert.Contains("Alice", success.Entries[0].MappedPayload);
        Assert.Contains("Email", success.Entries[0].MappedPayload);
        Assert.Contains("alice@test.com", success.Entries[0].MappedPayload);
    }

    [Fact]
    public void ApplyMapping_SingleTarget_ConstantTransform()
    {
        var success = VerifySharedStep2(
            input0: Dataset_ApplyMapping_SingleTarget_ConstantTransform_1,
            input1: Dataset_ApplyMapping_SingleTarget_ConstantTransform_2,
            value2: """{"Id":"u1","Name":"Alice"}""",
            value3: "Source"
        );
        Assert.Contains("mobile-app", success.Entries[0].MappedPayload);
    }

    [Fact]
    public void ApplyMapping_SingleTarget_ExcludesColumns()
    {
        var mapping = Dataset_ApplyMapping_SingleTarget_ExcludesColumns_1() with
        {
            ExcludedColumns = ["PasswordHash", "SecurityStamp"],
        };
        var success = VerifySharedChecks1(
            input0: mapping,
            value0: "User",
            value1: """{"Id":"u1"}""",
            value2: """{"Id":"u1","Name":"Alice","PasswordHash":"secret","SecurityStamp":"xyz"}"""
        );
        Assert.Contains("Name", success.Entries[0].MappedPayload);
        Assert.DoesNotContain("PasswordHash", success.Entries[0].MappedPayload);
        Assert.DoesNotContain("SecurityStamp", success.Entries[0].MappedPayload);
    }

    [Fact]
    public void ApplyMapping_DisabledMapping_ReturnsSkipped()
    {
        var mapping = Dataset_ApplyMapping_DisabledMapping_ReturnsSkipped_1() with
        {
            Enabled = false,
        };
        var config = MappingTestData.Strict(mappings: [mapping]);
        var entry = Dataset_ApplyMapping_DisabledMapping_ReturnsSkipped_2();

        var skipped = Expected<MappingSkipped>(entry: entry, config: config, logger: _logger);
        Assert.Contains("disabled", skipped.Reason);
    }

    [Fact]
    public void ApplyMapping_MultiTarget_ProducesMultipleEntries()
    {
        var success = VerifyUnifiedWorkflow102(
            input0: Dataset_ApplyMapping_MultiTarget_ProducesMultipleEntries_1,
            input1: Dataset_ApplyMapping_MultiTarget_ProducesMultipleEntries_2,
            value2: """{"Id":"o1","Total":99.99,"CustomerId":"c1"}"""
        );
        Assert.Equal(2, success.Entries.Count);
        Assert.Contains(success.Entries, e => e.TargetTable == "OrderHeader");
        Assert.Contains(success.Entries, e => e.TargetTable == "OrderAudit");
    }

    [Fact]
    public void ApplyMapping_MultiTarget_EachTargetHasCorrectPayload()
    {
        var success = VerifyUnifiedWorkflow102(
            input0: Dataset_ApplyMapping_MultiTarget_EachTargetHasCorrectPayload_1,
            input1: Dataset_ApplyMapping_MultiTarget_EachTargetHasCorrectPayload_2,
            value2: """{"Id":"o1","Total":99.99}"""
        );
        var header = success.Entries.First(e => e.TargetTable == "OrderHeader");
        var audit = success.Entries.First(e => e.TargetTable == "OrderAudit");

        Assert.Contains("Amount", header.MappedPayload);
        Assert.Contains("EventType", audit.MappedPayload);
        Assert.Contains("created", audit.MappedPayload);
    }

    [Fact]
    public void MapPrimaryKey_NullMapping_ReturnsSamePk()
    {
        var pk = """{"Id":"test-id"}""";

        var result = MappingEngine.MapPrimaryKey(pk, null);

        Assert.Equal(pk, result);
    }

    [Fact]
    public void MapPrimaryKey_WithMapping_RenamesColumn()
    {
        var pk = """{"Id":"test-id"}""";

        var result = MapCustomerIdPk(pk);

        ContainsAll(actual: result, expected: ["CustomerId", "test-id"]);
        Assert.DoesNotContain(":\"Id\"", result);
    }

    [Fact]
    public void MapPrimaryKey_SourceNotFound_ReturnsSamePk()
    {
        var pk = """{"OtherId":"test-id"}""";

        var result = MapCustomerIdPk(pk);

        Assert.Equal(pk, result);
    }

    [Fact]
    public void ApplyMapping_DeleteOperation_NullPayload_Succeeds()
    {
        _ = ExpectNullMappedPayload(
            mapping: Fixture_ApplyMapping_DeleteOperation_NullPayload_Succeeds_1,
            entry: Fixture_ApplyMapping_DeleteOperation_NullPayload_Succeeds_2
        );
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private MappingSuccess ExpectNullMappedPayload(
        Func<TableMapping> mapping,
        Func<SyncLogEntry> entry
    )
    {
        var config = MappingTestData.Strict(mappings: [mapping()]);
        var success = Expected<MappingSuccess>(entry: entry(), config: config, logger: _logger);
        Assert.Null(success.Entries[0].MappedPayload);
        return success;
    }

    private static string MapCustomerIdPk(string pk) =>
        MappingEngine.MapPrimaryKey(pk, new PkMapping("Id", "CustomerId"));

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private global::Nimblesite.Sync.Core.MappingSuccess ApplyConfiguredMappingToEntry(
        global::Nimblesite.Sync.Core.SyncMappingConfig input0,
        string value0,
        string value1,
        string value2
    )
    {
        var entry = MappingTestData.Entry(table: value0, pk: value1, payload: value2);

        var success = Expected<MappingSuccess>(entry: entry, config: input0, logger: _logger);

        return success;
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private MappingSuccess VerifySharedChecks1(
        TableMapping input0,
        string value0,
        string value1,
        string value2
    )
    {
        var config = MappingTestData.Strict(mappings: [input0]);
        var success = ApplyConfiguredMappingToEntry(
            input0: config,
            value0: value0,
            value1: value1,
            value2: value2
        );

        return success;
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private static TableMapping? VerifySharedChecks6(
        Func<TableMapping> input0,
        global::Nimblesite.Sync.Core.MappingDirection value1
    )
    {
        var mapping = input0();
        var config = MappingTestData.Strict(mappings: [mapping]);

        var found = MappingEngine.FindMapping("User", config, value1);

        return found;
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    internal static global::Nimblesite.Sync.Core.TableMapping VerifyCrossClassChecks1(
        Func<List<ColumnMapping>> input0,
        Func<List<ColumnMapping>, TableMapping> input1
    )
    {
        var columnMappings = input0();

        var mapping = input1(columnMappings);

        return mapping;
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    internal static global::Nimblesite.Sync.Core.TableMapping VerifyCrossClassChecks2(
        Func<List<TargetConfig>> input0,
        Func<TableMapping> input1
    )
    {
        var targets = input0();

        var mapping = input1() with { IsMultiTarget = true, Targets = targets };

        return mapping;
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    internal MappingSuccess VerifyUnifiedWorkflow102(
        Func<List<TargetConfig>> input0,
        Func<TableMapping> input1,
        string value2
    )
    {
        var mapping = VerifyCrossClassChecks2(input0: input0, input1: input1);
        var success = VerifySharedChecks1(
            input0: mapping,
            value0: "Order",
            value1: """{"Id":"o1"}""",
            value2: value2
        );

        return success;
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    internal MappingSuccess VerifySharedStep2(
        Func<List<ColumnMapping>> input0,
        Func<List<ColumnMapping>, TableMapping> input1,
        string value2,
        string value3
    )
    {
        var mapping = VerifyCrossClassChecks1(input0: input0, input1: input1);
        var success = VerifySharedChecks1(
            input0: mapping,
            value0: "User",
            value1: """{"Id":"u1"}""",
            value2: value2
        );
        Assert.Contains(value3, success.Entries[0].MappedPayload);

        return success;
    }

    // Implements [CI-TEST-SHARED-CASE-MATRIX].
    private static void ExerciseProjectionCaseMatrix4(
        Func<Func<TableMapping>, MappingDirection, TableMapping?> input0,
        Func<TableMapping> input1,
        string value1
    )
    {
        var found = input0(arg1: input1, arg2: MappingDirection.Push);

        Assert.NotNull(found);
        Assert.Equal(value1, found.Id);
    }

    // Implements [CI-TEST-SHARED-CASE-MATRIX].
    private static void ExerciseProjectionCaseMatrix5(
        Func<Func<TableMapping>, MappingDirection, TableMapping?> input0,
        Func<TableMapping> input1,
        global::Nimblesite.Sync.Core.MappingDirection value0
    )
    {
        var found = input0(arg1: input1, arg2: value0);

        Assert.Null(found);
    }
}
