using Microsoft.Extensions.Logging.Abstractions;

namespace Nimblesite.Sync.Tests;

// Implements [SYNC-MAPPING-TEST-DATA].

/// <summary>
/// Tests for MappingEngine - data transformation during sync.
/// Covers spec Section 7 - Data Mapping.
/// </summary>
public sealed class MappingEngineTests
{
    private readonly NullLogger<MappingEngineTests> _logger = new();

    [Fact]
    public void FindMapping_ExactMatch_ReturnsMapping()
    {
        var mapping = MappingTestData.Mapping(
            id: "user-to-customer",
            sourceTable: "User",
            targetTable: "Customer",
            direction: MappingDirection.Push
        );
        var config = MappingTestData.Strict(mappings: [mapping]);

        var found = MappingEngine.FindMapping("User", config, MappingDirection.Push);

        Assert.NotNull(found);
        Assert.Equal("user-to-customer", found.Id);
    }

    [Fact]
    public void FindMapping_BothDirection_MatchesPush()
    {
        var mapping = MappingTestData.Mapping(
            id: "user-both",
            sourceTable: "User",
            targetTable: "Customer",
            direction: MappingDirection.Both
        );
        var config = MappingTestData.Strict(mappings: [mapping]);

        var found = MappingEngine.FindMapping("User", config, MappingDirection.Push);

        Assert.NotNull(found);
        Assert.Equal("user-both", found.Id);
    }

    [Fact]
    public void FindMapping_BothDirection_MatchesPull()
    {
        var mapping = MappingTestData.Mapping(
            id: "user-both",
            sourceTable: "User",
            targetTable: "Customer",
            direction: MappingDirection.Both
        );
        var config = MappingTestData.Strict(mappings: [mapping]);

        var found = MappingEngine.FindMapping("User", config, MappingDirection.Pull);

        Assert.NotNull(found);
    }

    [Fact]
    public void FindMapping_WrongDirection_ReturnsNull()
    {
        var mapping = MappingTestData.Mapping(
            id: "user-push",
            sourceTable: "User",
            targetTable: "Customer",
            direction: MappingDirection.Push
        );
        var config = MappingTestData.Strict(mappings: [mapping]);

        var found = MappingEngine.FindMapping("User", config, MappingDirection.Pull);

        Assert.Null(found);
    }

    [Fact]
    public void FindMapping_UnmappedTable_ReturnsNull()
    {
        var mapping = MappingTestData.Mapping(
            id: "order-mapping",
            sourceTable: "Order",
            targetTable: "OrderSummary",
            direction: MappingDirection.Push
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

        var result = MappingEngine.ApplyMapping(entry, config, MappingDirection.Push, _logger);

        var success = Assert.IsType<MappingSuccess>(result);
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

        var result = MappingEngine.ApplyMapping(entry, config, MappingDirection.Push, _logger);

        var skipped = Assert.IsType<MappingSkipped>(result);
        Assert.Contains("Person", skipped.Reason);
    }

    [Fact]
    public void ApplyMapping_SingleTarget_RenamesTable()
    {
        var mapping = MappingTestData.Mapping(
            id: "user-to-customer",
            sourceTable: "User",
            targetTable: "Customer",
            direction: MappingDirection.Push
        );
        var config = MappingTestData.Strict(mappings: [mapping]);
        var entry = MappingTestData.Entry(
            table: "User",
            pk: """{"Id":"u1"}""",
            payload: """{"Id":"u1","Name":"Alice"}"""
        );

        var result = MappingEngine.ApplyMapping(entry, config, MappingDirection.Push, _logger);

        var success = Assert.IsType<MappingSuccess>(result);
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

        var result = MappingEngine.ApplyMapping(entry, config, MappingDirection.Push, _logger);

        var success = Assert.IsType<MappingSuccess>(result);
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

        var mapping = MappingTestData.Mapping(
            id: "user-mapping",
            sourceTable: "User",
            targetTable: "Customer",
            columnMappings: columnMappings
        );
        var config = MappingTestData.Strict(mappings: [mapping]);
        var entry = MappingTestData.Entry(
            table: "User",
            pk: """{"Id":"u1"}""",
            payload: """{"Id":"u1","FullName":"Alice","EmailAddress":"alice@test.com"}"""
        );

        var result = MappingEngine.ApplyMapping(entry, config, MappingDirection.Push, _logger);

        var success = Assert.IsType<MappingSuccess>(result);
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

        var mapping = MappingTestData.Mapping(
            id: "user-mapping",
            sourceTable: "User",
            targetTable: "Customer",
            columnMappings: columnMappings
        );
        var config = MappingTestData.Strict(mappings: [mapping]);
        var entry = MappingTestData.Entry(
            table: "User",
            pk: """{"Id":"u1"}""",
            payload: """{"Id":"u1","Name":"Alice"}"""
        );

        var result = MappingEngine.ApplyMapping(entry, config, MappingDirection.Push, _logger);

        var success = Assert.IsType<MappingSuccess>(result);
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

        var result = MappingEngine.ApplyMapping(entry, config, MappingDirection.Push, _logger);

        var success = Assert.IsType<MappingSuccess>(result);
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

        var result = MappingEngine.ApplyMapping(entry, config, MappingDirection.Push, _logger);

        var skipped = Assert.IsType<MappingSkipped>(result);
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

        var result = MappingEngine.ApplyMapping(entry, config, MappingDirection.Push, _logger);

        var success = Assert.IsType<MappingSuccess>(result);
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

        var result = MappingEngine.ApplyMapping(entry, config, MappingDirection.Push, _logger);

        var success = Assert.IsType<MappingSuccess>(result);
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
        var mapping = MappingTestData.Mapping(
            id: "user-mapping",
            sourceTable: "User",
            targetTable: "Customer",
            direction: MappingDirection.Push
        );
        var config = MappingTestData.Strict(mappings: [mapping]);
        var entry = new SyncLogEntry(
            Version: 1,
            TableName: "User",
            PkValue: """{"Id":"u1"}""",
            Operation: SyncOperation.Delete,
            Payload: null,
            Origin: "origin-1",
            Timestamp: "2024-01-01T00:00:00Z"
        );

        var result = MappingEngine.ApplyMapping(entry, config, MappingDirection.Push, _logger);

        var success = Assert.IsType<MappingSuccess>(result);
        Assert.Null(success.Entries[0].MappedPayload);
    }
}
