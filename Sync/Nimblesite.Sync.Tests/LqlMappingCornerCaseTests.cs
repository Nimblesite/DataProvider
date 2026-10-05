using Microsoft.Extensions.Logging.Abstractions;

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
        var mapping = MappingTestData.Mapping(
            id: "test",
            sourceTable: "Source",
            targetTable: "Target",
            columnMappings:
            [
                new ColumnMapping("Name", "DisplayName", TransformType.Lql, null, "upper(Name)"),
            ]
        );
        var config = MappingTestData.Strict(mappings: [mapping]);

        var entry = new SyncLogEntry(
            Version: 1,
            TableName: "Source",
            PkValue: """{"Id":"1"}""",
            Operation: SyncOperation.Delete,
            Payload: null, // DELETE operations have null payload
            Origin: "test",
            Timestamp: "2024-01-01T00:00:00Z"
        );

        var result = MappingEngine.ApplyMapping(entry, config, MappingDirection.Push, _logger);

        var success = Assert.IsType<MappingSuccess>(result);
        Assert.Null(success.Entries[0].MappedPayload);
    }

    [Fact]
    public void MappingEngine_EmptyColumnMappings_PassthroughPayload()
    {
        var mapping = MappingTestData.Mapping(
            id: "passthrough",
            sourceTable: "Source",
            targetTable: "Target"
        );
        var config = MappingTestData.Strict(mappings: [mapping]);

        var entry = MappingTestData.Entry(
            table: "Source",
            pk: """{"Id":"1"}""",
            payload: """{"Id":"1","Name":"Test","Value":123}"""
        );

        var result = MappingEngine.ApplyMapping(entry, config, MappingDirection.Push, _logger);

        var success = Assert.IsType<MappingSuccess>(result);
        var payload = success.Entries[0].MappedPayload!;
        Assert.Contains("Name", payload);
        Assert.Contains("Test", payload);
        Assert.Contains("Value", payload);
    }

    [Fact]
    public void MappingEngine_LqlWithMissingSourceColumn_FallsBack()
    {
        var columnMappings = new List<ColumnMapping>
        {
            new("NonExistent", "DisplayName", TransformType.Lql, null, "upper(NonExistent)"),
        };

        var mapping = MappingTestData.Mapping(
            id: "test",
            sourceTable: "Source",
            targetTable: "Target",
            columnMappings: columnMappings
        );
        var config = MappingTestData.Strict(mappings: [mapping]);

        var entry = MappingTestData.Entry(
            table: "Source",
            pk: """{"Id":"1"}""",
            payload: """{"Id":"1","Name":"Alice"}"""
        );

        // Should not throw, should handle gracefully
        var result = MappingEngine.ApplyMapping(entry, config, MappingDirection.Push, _logger);

        var success = Assert.IsType<MappingSuccess>(result);
        Assert.NotNull(success.Entries[0].MappedPayload);
    }

    [Fact]
    public void MappingEngine_MixedTransformTypes_AllApplied()
    {
        var columnMappings = new List<ColumnMapping>
        {
            new("Name", "Name"), // Direct mapping
            new("Email", "NormalizedEmail", TransformType.Lql, null, "lower(Email)"), // LQL transform
            new(null, "Source", TransformType.Constant, "mobile-app"), // Constant
            new("Status", "StatusCode"), // Direct mapping
        };

        var mapping = MappingTestData.Mapping(
            id: "test",
            sourceTable: "Source",
            targetTable: "Target",
            columnMappings: columnMappings
        );
        var config = MappingTestData.Strict(mappings: [mapping]);

        var entry = MappingTestData.Entry(
            table: "Source",
            pk: """{"Id":"1"}""",
            payload: """{"Id":"1","Name":"Alice","Email":"ALICE@TEST.COM","Status":"active"}"""
        );

        var result = MappingEngine.ApplyMapping(entry, config, MappingDirection.Push, _logger);

        var success = Assert.IsType<MappingSuccess>(result);
        var payload = success.Entries[0].MappedPayload!;
        Assert.Contains("Alice", payload); // Direct
        Assert.Contains("alice@test.com", payload); // LQL lowercase
        Assert.Contains("mobile-app", payload); // Constant
        Assert.Contains("active", payload); // Direct
    }

    [Fact]
    public void MappingEngine_ExcludedColumns_RemovedFromPayload()
    {
        var mapping = MappingTestData.Mapping(
            id: "with-exclusions",
            sourceTable: "User",
            targetTable: "PublicUser"
        ) with
        {
            ExcludedColumns = ["Password", "Salt", "SecurityToken", "InternalNotes"],
        };
        var config = MappingTestData.Strict(mappings: [mapping]);

        var entry = MappingTestData.Entry(
            table: "User",
            pk: """{"Id":"1"}""",
            payload: """{"Id":"1","Name":"Alice","Email":"a@test.com","Password":"secret123","Salt":"xyz","SecurityToken":"abc","InternalNotes":"admin notes","PublicBio":"Hello!"}"""
        );

        var result = MappingEngine.ApplyMapping(entry, config, MappingDirection.Push, _logger);

        var success = Assert.IsType<MappingSuccess>(result);
        var payload = success.Entries[0].MappedPayload!;

        Assert.Contains("Name", payload);
        Assert.Contains("Email", payload);
        Assert.Contains("PublicBio", payload);
        Assert.DoesNotContain("Password", payload);
        Assert.DoesNotContain("Salt", payload);
        Assert.DoesNotContain("SecurityToken", payload);
        Assert.DoesNotContain("InternalNotes", payload);
    }

    [Fact]
    public void MappingEngine_PkMapping_RenamesKey()
    {
        var mapping = MappingTestData.Mapping(
            id: "pk-rename",
            sourceTable: "Source",
            targetTable: "Target"
        ) with
        {
            PkMapping = new PkMapping("UserId", "CustomerId"),
        };
        var config = MappingTestData.Strict(mappings: [mapping]);

        var entry = MappingTestData.Entry(
            table: "Source",
            pk: """{"UserId":"user-123"}""",
            payload: """{"Name":"Test"}"""
        );

        var result = MappingEngine.ApplyMapping(entry, config, MappingDirection.Push, _logger);

        var success = Assert.IsType<MappingSuccess>(result);
        Assert.Contains("CustomerId", success.Entries[0].TargetPkValue);
        Assert.Contains("user-123", success.Entries[0].TargetPkValue);
        Assert.DoesNotContain("UserId", success.Entries[0].TargetPkValue);
    }

    [Fact]
    public void MappingEngine_MultiTarget_AllTargetsReceiveTransformedData()
    {
        var targets = new List<TargetConfig>
        {
            new(
                "UserProfile",
                [
                    new ColumnMapping(
                        "Name",
                        "DisplayName",
                        TransformType.Lql,
                        null,
                        "upper(Name)"
                    ),
                    new ColumnMapping("Email", "Email"),
                ]
            ),
            new(
                "UserAudit",
                [
                    new ColumnMapping("Name", "UserName"),
                    new ColumnMapping(null, "EventType", TransformType.Constant, "user_created"),
                ]
            ),
        };

        var mapping = MappingTestData.Mapping(
            id: "multi-target",
            sourceTable: "User",
            targetTable: null
        ) with
        {
            IsMultiTarget = true,
            Targets = targets,
        };
        var config = MappingTestData.Strict(mappings: [mapping]);

        var entry = MappingTestData.Entry(
            table: "User",
            pk: """{"Id":"1"}""",
            payload: """{"Id":"1","Name":"alice","Email":"alice@test.com"}"""
        );

        var result = MappingEngine.ApplyMapping(entry, config, MappingDirection.Push, _logger);

        var success = Assert.IsType<MappingSuccess>(result);
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
        var columnMappings = new List<ColumnMapping>
        {
            new("street", "Street", TransformType.Lql, null, "trim(street)"),
            new("city", "City", TransformType.Lql, null, "city |> trim() |> upper()"),
            new("state", "State", TransformType.Lql, null, "upper(state)"),
            new("zip", "PostalCode", TransformType.Lql, null, "left(zip, 5)"),
        };

        var mapping = MappingTestData.Mapping(
            id: "address-norm",
            sourceTable: "RawAddress",
            targetTable: "Address",
            columnMappings: columnMappings
        );
        var config = MappingTestData.Strict(mappings: [mapping]);

        var entry = MappingTestData.Entry(
            table: "RawAddress",
            pk: """{"Id":"1"}""",
            payload: """{"Id":"1","street":"  123 Main St  ","city":"  springfield  ","state":"il","zip":"62701-1234"}"""
        );

        var result = MappingEngine.ApplyMapping(entry, config, MappingDirection.Push, _logger);

        var success = Assert.IsType<MappingSuccess>(result);
        var payload = success.Entries[0].MappedPayload!;

        Assert.Contains("123 Main St", payload); // Trimmed street
        Assert.Contains("SPRINGFIELD", payload); // Trimmed + uppercased city
        Assert.Contains("IL", payload); // Uppercased state
        Assert.Contains("62701", payload); // First 5 chars of zip
        Assert.DoesNotContain("-1234", payload); // ZIP+4 removed
    }

    [Fact]
    public void E2E_UserDataSanitization()
    {
        // Remove sensitive fields, normalize remaining
        var columnMappings = new List<ColumnMapping>
        {
            new("username", "Username", TransformType.Lql, null, "lower(username)"),
            new("email", "Email", TransformType.Lql, null, "lower(email)"),
            new("display_name", "DisplayName", TransformType.Lql, null, "trim(display_name)"),
            // Constant for sync source
            new(null, "DataSource", TransformType.Constant, "legacy_crm"),
        };

        var mapping = MappingTestData.Mapping(
            id: "user-sanitize",
            sourceTable: "LegacyUser",
            targetTable: "CleanUser",
            columnMappings: columnMappings
        ) with
        {
            PkMapping = new PkMapping("legacy_id", "Id"),
            ExcludedColumns = ["password_hash", "ssn", "credit_card"],
        };
        var config = MappingTestData.Strict(mappings: [mapping]);

        var entry = MappingTestData.Entry(
            table: "LegacyUser",
            pk: """{"legacy_id":"USR-001"}""",
            payload: """{"legacy_id":"USR-001","username":"JohnDoe123","email":"John.Doe@Example.COM","display_name":"  John Doe  ","password_hash":"abc123","ssn":"123-45-6789","credit_card":"4111111111111111"}"""
        );

        var result = MappingEngine.ApplyMapping(entry, config, MappingDirection.Push, _logger);

        var success = Assert.IsType<MappingSuccess>(result);
        var payload = success.Entries[0].MappedPayload!;

        // PK renamed
        Assert.Contains("Id", success.Entries[0].TargetPkValue);
        Assert.Contains("USR-001", success.Entries[0].TargetPkValue);

        // Normalized fields
        Assert.Contains("johndoe123", payload); // Lowercase username
        Assert.Contains("john.doe@example.com", payload); // Lowercase email
        Assert.Contains("John Doe", payload); // Trimmed display name

        // Sensitive fields excluded
        Assert.DoesNotContain("password", payload);
        Assert.DoesNotContain("ssn", payload);
        Assert.DoesNotContain("credit_card", payload);
        Assert.DoesNotContain("4111", payload);
    }

    #endregion
}
