using Microsoft.Extensions.Logging.Abstractions;
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

    [Fact]
    public void FindMapping_ExactMatch_ReturnsMapping()
    {
        var mapping = Fixture_FindMapping_ExactMatch_ReturnsMapping_1();
        var config = MappingTestData.Strict(mappings: [mapping]);

        var found = MappingEngine.FindMapping("User", config, MappingDirection.Push);

        Assert.NotNull(found);
        Assert.Equal("user-to-customer", found.Id);
    }

    [Fact]
    public void FindMapping_BothDirection_MatchesPush()
    {
        var mapping = Fixture_FindMapping_BothDirection_MatchesPush_1();
        var config = MappingTestData.Strict(mappings: [mapping]);

        var found = MappingEngine.FindMapping("User", config, MappingDirection.Push);

        Assert.NotNull(found);
        Assert.Equal("user-both", found.Id);
    }

    [Fact]
    public void FindMapping_BothDirection_MatchesPull()
    {
        var mapping = Fixture_FindMapping_BothDirection_MatchesPull_1();
        var config = MappingTestData.Strict(mappings: [mapping]);

        var found = MappingEngine.FindMapping("User", config, MappingDirection.Pull);

        Assert.NotNull(found);
    }

    [Fact]
    public void FindMapping_WrongDirection_ReturnsNull()
    {
        var mapping = Fixture_FindMapping_WrongDirection_ReturnsNull_1();
        var config = MappingTestData.Strict(mappings: [mapping]);

        var found = MappingEngine.FindMapping("User", config, MappingDirection.Pull);

        Assert.Null(found);
    }

    [Fact]
    public void FindMapping_UnmappedTable_ReturnsNull()
    {
        var mapping = Fixture_FindMapping_UnmappedTable_ReturnsNull_1();
        var config = MappingTestData.Strict(mappings: [mapping]);

        var found = MappingEngine.FindMapping("User", config, MappingDirection.Push);

        Assert.Null(found);
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
        var config = MappingTestData.Strict(mappings: [mapping]);
        var success = ApplyConfiguredMappingToEntry(
            input0: config,
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
        var config = MappingTestData.Strict(mappings: [mapping]);
        var success = ApplyConfiguredMappingToEntry(
            input0: config,
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
        var columnMappings = Dataset_ApplyMapping_SingleTarget_MapsColumns_1();

        var mapping = Dataset_ApplyMapping_SingleTarget_MapsColumns_2(
            columnMappings: columnMappings
        );
        var config = MappingTestData.Strict(mappings: [mapping]);
        var success = ApplyConfiguredMappingToEntry(
            input0: config,
            value0: "User",
            value1: """{"Id":"u1"}""",
            value2: """{"Id":"u1","FullName":"Alice","EmailAddress":"alice@test.com"}"""
        );
        Assert.Contains("Name", success.Entries[0].MappedPayload);
        Assert.Contains("Alice", success.Entries[0].MappedPayload);
        Assert.Contains("Email", success.Entries[0].MappedPayload);
        Assert.Contains("alice@test.com", success.Entries[0].MappedPayload);
    }

    [Fact]
    public void ApplyMapping_SingleTarget_ConstantTransform()
    {
        var columnMappings = Dataset_ApplyMapping_SingleTarget_ConstantTransform_1();

        var mapping = Dataset_ApplyMapping_SingleTarget_ConstantTransform_2(
            columnMappings: columnMappings
        );
        var config = MappingTestData.Strict(mappings: [mapping]);
        var success = ApplyConfiguredMappingToEntry(
            input0: config,
            value0: "User",
            value1: """{"Id":"u1"}""",
            value2: """{"Id":"u1","Name":"Alice"}"""
        );
        Assert.Contains("Source", success.Entries[0].MappedPayload);
        Assert.Contains("mobile-app", success.Entries[0].MappedPayload);
    }

    [Fact]
    public void ApplyMapping_SingleTarget_ExcludesColumns()
    {
        var mapping = Dataset_ApplyMapping_SingleTarget_ExcludesColumns_1() with
        {
            ExcludedColumns = ["PasswordHash", "SecurityStamp"],
        };
        var config = MappingTestData.Strict(mappings: [mapping]);
        var success = ApplyConfiguredMappingToEntry(
            input0: config,
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
        var targets = Dataset_ApplyMapping_MultiTarget_ProducesMultipleEntries_1();

        var mapping = Dataset_ApplyMapping_MultiTarget_ProducesMultipleEntries_2() with
        {
            IsMultiTarget = true,
            Targets = targets,
        };
        var config = MappingTestData.Strict(mappings: [mapping]);
        var success = ApplyConfiguredMappingToEntry(
            input0: config,
            value0: "Order",
            value1: """{"Id":"o1"}""",
            value2: """{"Id":"o1","Total":99.99,"CustomerId":"c1"}"""
        );
        Assert.Equal(2, success.Entries.Count);
        Assert.Contains(success.Entries, e => e.TargetTable == "OrderHeader");
        Assert.Contains(success.Entries, e => e.TargetTable == "OrderAudit");
    }

    [Fact]
    public void ApplyMapping_MultiTarget_EachTargetHasCorrectPayload()
    {
        var targets = Dataset_ApplyMapping_MultiTarget_EachTargetHasCorrectPayload_1();

        var mapping = Dataset_ApplyMapping_MultiTarget_EachTargetHasCorrectPayload_2() with
        {
            IsMultiTarget = true,
            Targets = targets,
        };
        var config = MappingTestData.Strict(mappings: [mapping]);
        var success = ApplyConfiguredMappingToEntry(
            input0: config,
            value0: "Order",
            value1: """{"Id":"o1"}""",
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
        var mapping = new PkMapping("Id", "CustomerId");

        var result = MappingEngine.MapPrimaryKey(pk, mapping);

        ContainsAll(actual: result, expected: ["CustomerId", "test-id"]);
        Assert.DoesNotContain(":\"Id\"", result);
    }

    [Fact]
    public void MapPrimaryKey_SourceNotFound_ReturnsSamePk()
    {
        var pk = """{"OtherId":"test-id"}""";
        var mapping = new PkMapping("Id", "CustomerId");

        var result = MappingEngine.MapPrimaryKey(pk, mapping);

        Assert.Equal(pk, result);
    }

    [Fact]
    public void ApplyMapping_DeleteOperation_NullPayload_Succeeds()
    {
        var mapping = Fixture_ApplyMapping_DeleteOperation_NullPayload_Succeeds_1();
        var config = MappingTestData.Strict(mappings: [mapping]);
        var entry = Fixture_ApplyMapping_DeleteOperation_NullPayload_Succeeds_2();

        var success = Expected<MappingSuccess>(entry: entry, config: config, logger: _logger);
        Assert.Null(success.Entries[0].MappedPayload);
    }

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
}
