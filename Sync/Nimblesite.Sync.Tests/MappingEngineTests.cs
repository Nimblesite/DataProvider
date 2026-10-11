using Microsoft.Extensions.Logging.Abstractions;
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
        var mapping = Fixture_FindMapping_ExactMatch_ReturnsMapping_1(
            id: id,
            sourceTable: sourceTable,
            targetTable: targetTable,
            direction: direction
        );
        var config = MappingTestData.Strict(mappings: [mapping]);

        var found = MappingEngine.FindMapping("User", config, MappingDirection.Push);

        Assert.NotNull(found);
        Assert.Equal("user-to-customer", found.Id);
    }

    [Fact]
    public void FindMapping_BothDirection_MatchesPush()
    {
        var mapping = Fixture_FindMapping_BothDirection_MatchesPush_1(
            id: id,
            sourceTable: sourceTable,
            targetTable: targetTable,
            direction: direction
        );
        var config = MappingTestData.Strict(mappings: [mapping]);

        var found = MappingEngine.FindMapping("User", config, MappingDirection.Push);

        Assert.NotNull(found);
        Assert.Equal("user-both", found.Id);
    }

    [Fact]
    public void FindMapping_BothDirection_MatchesPull()
    {
        var mapping = Fixture_FindMapping_BothDirection_MatchesPull_1(
            id: id,
            sourceTable: sourceTable,
            targetTable: targetTable,
            direction: direction
        );
        var config = MappingTestData.Strict(mappings: [mapping]);

        var found = MappingEngine.FindMapping("User", config, MappingDirection.Pull);

        Assert.NotNull(found);
    }

    [Fact]
    public void FindMapping_WrongDirection_ReturnsNull()
    {
        var mapping = Fixture_FindMapping_WrongDirection_ReturnsNull_1(
            id: id,
            sourceTable: sourceTable,
            targetTable: targetTable,
            direction: direction
        );
        var config = MappingTestData.Strict(mappings: [mapping]);

        var found = MappingEngine.FindMapping("User", config, MappingDirection.Pull);

        Assert.Null(found);
    }

    [Fact]
    public void FindMapping_UnmappedTable_ReturnsNull()
    {
        var mapping = Fixture_FindMapping_UnmappedTable_ReturnsNull_1(
            id: id,
            sourceTable: sourceTable,
            targetTable: targetTable,
            direction: direction
        );
        var config = MappingTestData.Strict(mappings: [mapping]);

        var found = MappingEngine.FindMapping("User", config, MappingDirection.Push);

        Assert.Null(found);
    }

    [Fact]
    public void ApplyMapping_PassthroughMode_NoMapping_ReturnsIdentity()
    {
        var config = SyncMappingConfig.Passthrough;
        var entry = MappingTestData.Entry(
            table: "Person",
            pk: """{"Id":"p1"}""",
            payload: """{"Id":"p1","Name":"Alice"}"""
        );

        var success = Expected<MappingSuccess>(entry: entry, config: config, logger: _logger);
        Assert.Single(success.Entries);
        Assert.Equal("Person", success.Entries[0].TargetTable);
        Assert.Equal("""{"Id":"p1"}""", success.Entries[0].TargetPkValue);
    }

    [Fact]
    public void ApplyMapping_StrictMode_NoMapping_ReturnsSkipped()
    {
        var config = SyncMappingConfig.Empty;
        var entry = MappingTestData.Entry(
            table: "Person",
            pk: """{"Id":"p1"}""",
            payload: """{"Id":"p1","Name":"Alice"}"""
        );

        var skipped = Expected<MappingSkipped>(entry: entry, config: config, logger: _logger);
        Assert.Contains("Person", skipped.Reason);
    }

    [Fact]
    public void ApplyMapping_SingleTarget_RenamesTable()
    {
        var mapping = Fixture_ApplyMapping_SingleTarget_RenamesTable_1(
            id: id,
            sourceTable: sourceTable,
            targetTable: targetTable,
            direction: direction
        );
        var config = MappingTestData.Strict(mappings: [mapping]);
        var entry = MappingTestData.Entry(
            table: "User",
            pk: """{"Id":"u1"}""",
            payload: """{"Id":"u1","Name":"Alice"}"""
        );

        var success = Expected<MappingSuccess>(entry: entry, config: config, logger: _logger);
        Assert.Single(success.Entries);
        Assert.Equal("Customer", success.Entries[0].TargetTable);
    }

    [Fact]
    public void ApplyMapping_SingleTarget_MapsPrimaryKey()
    {
        var mapping = MappingTestData.Mapping(
            id: "user-mapping",
            sourceTable: "User",
            targetTable: "Customer"
        ) with
        {
            PkMapping = new PkMapping("Id", "CustomerId"),
        };
        var config = MappingTestData.Strict(mappings: [mapping]);
        var entry = MappingTestData.Entry(
            table: "User",
            pk: """{"Id":"u1"}""",
            payload: """{"Id":"u1","Name":"Alice"}"""
        );

        var success = Expected<MappingSuccess>(entry: entry, config: config, logger: _logger);
        Assert.Contains("CustomerId", success.Entries[0].TargetPkValue);
        Assert.Contains("u1", success.Entries[0].TargetPkValue);
    }

    [Fact]
    public void ApplyMapping_SingleTarget_MapsColumns()
    {
        var columnMappings = new List<ColumnMapping>
        {
            new("FullName", "Name"),
            new("EmailAddress", "Email"),
        };

        var mapping = Fixture_ApplyMapping_SingleTarget_MapsColumns_1(
            id: id,
            sourceTable: sourceTable,
            targetTable: targetTable,
            columnMappings: columnMappings,
            columnMappings: columnMappings
        );
        var config = MappingTestData.Strict(mappings: [mapping]);
        var entry = MappingTestData.Entry(
            table: "User",
            pk: """{"Id":"u1"}""",
            payload: """{"Id":"u1","FullName":"Alice","EmailAddress":"alice@test.com"}"""
        );

        var success = Expected<MappingSuccess>(entry: entry, config: config, logger: _logger);
        Assert.Contains("Name", success.Entries[0].MappedPayload);
        Assert.Contains("Alice", success.Entries[0].MappedPayload);
        Assert.Contains("Email", success.Entries[0].MappedPayload);
        Assert.Contains("alice@test.com", success.Entries[0].MappedPayload);
    }

    [Fact]
    public void ApplyMapping_SingleTarget_ConstantTransform()
    {
        var columnMappings = new List<ColumnMapping>
        {
            new("Name", "Name"),
            new(null, "Source", TransformType.Constant, "mobile-app"),
        };

        var mapping = Fixture_ApplyMapping_SingleTarget_ConstantTransform_1(
            id: id,
            sourceTable: sourceTable,
            targetTable: targetTable,
            columnMappings: columnMappings,
            columnMappings: columnMappings
        );
        var config = MappingTestData.Strict(mappings: [mapping]);
        var entry = MappingTestData.Entry(
            table: "User",
            pk: """{"Id":"u1"}""",
            payload: """{"Id":"u1","Name":"Alice"}"""
        );

        var success = Expected<MappingSuccess>(entry: entry, config: config, logger: _logger);
        Assert.Contains("Source", success.Entries[0].MappedPayload);
        Assert.Contains("mobile-app", success.Entries[0].MappedPayload);
    }

    [Fact]
    public void ApplyMapping_SingleTarget_ExcludesColumns()
    {
        var mapping = MappingTestData.Mapping(
            id: "user-mapping",
            sourceTable: "User",
            targetTable: "Customer"
        ) with
        {
            ExcludedColumns = ["PasswordHash", "SecurityStamp"],
        };
        var config = MappingTestData.Strict(mappings: [mapping]);
        var entry = MappingTestData.Entry(
            table: "User",
            pk: """{"Id":"u1"}""",
            payload: """{"Id":"u1","Name":"Alice","PasswordHash":"secret","SecurityStamp":"xyz"}"""
        );

        var success = Expected<MappingSuccess>(entry: entry, config: config, logger: _logger);
        Assert.Contains("Name", success.Entries[0].MappedPayload);
        Assert.DoesNotContain("PasswordHash", success.Entries[0].MappedPayload);
        Assert.DoesNotContain("SecurityStamp", success.Entries[0].MappedPayload);
    }

    [Fact]
    public void ApplyMapping_DisabledMapping_ReturnsSkipped()
    {
        var mapping = MappingTestData.Mapping(
            id: "disabled-mapping",
            sourceTable: "User",
            targetTable: "Customer"
        ) with
        {
            Enabled = false,
        };
        var config = MappingTestData.Strict(mappings: [mapping]);
        var entry = MappingTestData.Entry(
            table: "User",
            pk: """{"Id":"u1"}""",
            payload: """{"Id":"u1"}"""
        );

        var skipped = Expected<MappingSkipped>(entry: entry, config: config, logger: _logger);
        Assert.Contains("disabled", skipped.Reason);
    }

    [Fact]
    public void ApplyMapping_MultiTarget_ProducesMultipleEntries()
    {
        var targets = new List<TargetConfig>
        {
            new(
                "OrderHeader",
                [new ColumnMapping("Id", "OrderId"), new ColumnMapping("Total", "Amount")]
            ),
            new(
                "OrderAudit",
                [
                    new ColumnMapping("Id", "OrderId"),
                    new ColumnMapping(null, "EventType", TransformType.Constant, "created"),
                ]
            ),
        };

        var mapping = MappingTestData.Mapping(
            id: "order-split",
            sourceTable: "Order",
            targetTable: null
        ) with
        {
            IsMultiTarget = true,
            Targets = targets,
        };
        var config = MappingTestData.Strict(mappings: [mapping]);
        var entry = MappingTestData.Entry(
            table: "Order",
            pk: """{"Id":"o1"}""",
            payload: """{"Id":"o1","Total":99.99,"CustomerId":"c1"}"""
        );

        var success = Expected<MappingSuccess>(entry: entry, config: config, logger: _logger);
        Assert.Equal(2, success.Entries.Count);
        Assert.Contains(success.Entries, e => e.TargetTable == "OrderHeader");
        Assert.Contains(success.Entries, e => e.TargetTable == "OrderAudit");
    }

    [Fact]
    public void ApplyMapping_MultiTarget_EachTargetHasCorrectPayload()
    {
        var targets = new List<TargetConfig>
        {
            new("OrderHeader", [new ColumnMapping("Total", "Amount")]),
            new(
                "OrderAudit",
                [new ColumnMapping(null, "EventType", TransformType.Constant, "created")]
            ),
        };

        var mapping = MappingTestData.Mapping(
            id: "order-split",
            sourceTable: "Order",
            targetTable: null
        ) with
        {
            IsMultiTarget = true,
            Targets = targets,
        };
        var config = MappingTestData.Strict(mappings: [mapping]);
        var entry = MappingTestData.Entry(
            table: "Order",
            pk: """{"Id":"o1"}""",
            payload: """{"Id":"o1","Total":99.99}"""
        );

        var success = Expected<MappingSuccess>(entry: entry, config: config, logger: _logger);
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

        Assert.Contains("CustomerId", result);
        Assert.Contains("test-id", result);
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
        var mapping = Fixture_ApplyMapping_DeleteOperation_NullPayload_Succeeds_1(
            id: id,
            sourceTable: sourceTable,
            targetTable: targetTable,
            direction: direction
        );
        var config = MappingTestData.Strict(mappings: [mapping]);
        var entry = Fixture_ApplyMapping_DeleteOperation_NullPayload_Succeeds_2(
            Version: Version,
            TableName: TableName,
            PkValue: PkValue,
            Operation: Operation,
            Payload: Payload,
            Origin: Origin,
            Timestamp: Timestamp
        );

        var success = Expected<MappingSuccess>(entry: entry, config: config, logger: _logger);
        Assert.Null(success.Entries[0].MappedPayload);
    }
}
