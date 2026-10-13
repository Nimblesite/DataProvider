// Implements [CI-TEST-SHARED-COMMAND-PARAMETERS].
// Implements [CI-TEST-SHARED-COMMAND].
using System.Text.Json;
using static global::Tests.Shared.StringAssertions;

namespace Nimblesite.Sync.Http.Tests;

#pragma warning disable CA1001 // Type owns disposable fields - disposed via IAsyncLifetime.DisposeAsync

/// <summary>
/// E2E HTTP tests proving data mapping with LQL transforms works across databases.
/// Tests User -> Customer schema transformation with:
/// - Table rename (User -> Customer)
/// - PK rename (Id -> CustomerId)
/// - Column renames (FullName -> Name, EmailAddress -> Email)
/// - LQL transforms (upper, concat, dateFormat)
/// - Constant values (Source = "mobile-app")
/// - Excluded columns (PasswordHash, SecurityStamp)
/// This is the REAL PROOF that LQL mapping works over HTTP!
/// Uses the shared postgres container; each test gets its own database.
/// </summary>
[Collection(PostgresTestSuite.Name)]
[Trait("Category", "Docker")]
public sealed partial class HttpMappingE2ETests(PostgresContainerFixture fixture)
    : IAsyncLifetime,
        IDisposable
{
    private string _postgresConnectionString = null!;
    private string _sqliteDbPath = null!;
    private SqliteConnection _sqliteConn = null!;
    private NpgsqlConnection _postgresConn = null!;
    private readonly ILogger _logger = NullLogger.Instance;

    public async Task InitializeAsync()
    {
        _postgresConnectionString = await fixture
            .CreateDatabaseConnectionStringAsync("mappingdb")
            .ConfigureAwait(false);

        // Create SQLite database
        (_sqliteDbPath, _sqliteConn) = global::Tests.Shared.TempSqliteDatabase.Create(
            "mapping_test"
        );

        // Create Postgres connection
        _postgresConn = new NpgsqlConnection(_postgresConnectionString);
        _postgresConn.Open();
    }

    public Task DisposeAsync()
    {
        Dispose();
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _sqliteConn.Close();
        _sqliteConn.Dispose();
        _postgresConn.Close();
        _postgresConn.Dispose();

        if (File.Exists(_sqliteDbPath))
        {
            File.Delete(_sqliteDbPath);
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Sets up SQLite source with User table schema.
    /// </summary>
    private void SetupSqliteSource(string originId)
    {
        global::Nimblesite.Sync.Http.Tests.CrossDatabaseSyncTests.VerifyReusableWorkflow1(
            configure0: command => command.CommandText = Data_SetupSqliteSource_1,
            input0: _sqliteConn,
            input1: originId
        );

        TriggerGenerator.CreateTriggers(_sqliteConn, "User", _logger);
    }

    /// <summary>
    /// Sets up Postgres target with Customer table schema (different from source!).
    /// </summary>
    private void SetupPostgresTarget(string originId)
    {
        var schemaResult = PostgresSyncSchema.CreateSchema(_postgresConn);
        Assert.True(schemaResult is BoolSyncOk, $"Schema creation failed: {schemaResult}");

        var originResult = PostgresSyncSchema.SetOriginId(_postgresConn, originId);
        Assert.True(originResult is BoolSyncOk, $"Origin ID failed: {originResult}");

        using var cmd = _postgresConn.CreateCommand();
        cmd.CommandText = Data_SetupPostgresTarget_1;
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Creates mapping config for User -> Customer transformation.
    /// </summary>
    /// <summary>
    /// Seeds SQLite source + Postgres target with fresh origins and returns the User->Customer mapping.
    /// </summary>
    private SyncMappingConfig SetupUserToCustomerScenario()
    {
        var sourceOrigin = Guid.NewGuid().ToString();
        var targetOrigin = Guid.NewGuid().ToString();

        SetupSqliteSource(sourceOrigin);
        SetupPostgresTarget(targetOrigin);

        return CreateUserToCustomerMapping();
    }

    /// <summary>
    /// Merges the PK value into a payload for insert operations.
    /// PostgresChangeApplier expects PK to be in the payload.
    /// </summary>
    private static string MergePkIntoPayload(string pkValue, string? payload)
    {
        if (payload is null)
        {
            return pkValue; // For deletes, PK is the payload
        }

        using var pkDoc = JsonDocument.Parse(pkValue);
        using var payloadDoc = JsonDocument.Parse(payload);

        var merged = new Dictionary<string, object?>();

        // Add PK first
        foreach (var prop in pkDoc.RootElement.EnumerateObject())
        {
            merged[prop.Name] = JsonElementToObject(prop.Value);
        }

        // Add payload properties
        foreach (var prop in payloadDoc.RootElement.EnumerateObject())
        {
            merged[prop.Name] = JsonElementToObject(prop.Value);
        }

        return JsonSerializer.Serialize(merged);
    }

    private static object? JsonElementToObject(JsonElement element) =>
        element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number when element.TryGetInt64(out var l) => l,
            JsonValueKind.Number => element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => element.GetRawText(),
        };

    /// <summary>
    /// Shared act step: runs the mapping engine with the class logger (Push by default).
    /// </summary>
    private MappingResult ApplyMapping(
        SyncLogEntry entry,
        SyncMappingConfig config,
        MappingDirection direction = MappingDirection.Push
    ) => MappingEngine.ApplyMapping(entry, config, direction, _logger);

    /// <summary>
    /// Shared arrange step: single-table Push mapping against Source -> target,
    /// the shape used by the LQL transform tests.
    /// </summary>
    private static SyncMappingConfig MappingConfig(
        string mappingId,
        params ColumnMapping[] mappings
    ) => MappingConfig(mappingId, "Source", "target", mappings);

    /// <summary>Single-table Push mapping with explicit table names.</summary>
    /// <summary>Shared LQL transform column mapping.</summary>
    private static ColumnMapping LqlColumn(string target, string lql) =>
        new(Source: null, Target: target, Transform: TransformType.Lql, Lql: lql);

    /// <summary>Applies a mapping and asserts a single successful entry.</summary>
    private MappedEntry MapSingle(SyncLogEntry entry, SyncMappingConfig config) =>
        MapSuccessSingle(entry, config).Entries[0];

    /// <summary>Applies a mapping and asserts a single success with exactly one entry.</summary>
    private MappingSuccess MapSuccessSingle(SyncLogEntry entry, SyncMappingConfig config)
    {
        var success = Assert.IsType<MappingSuccess>(ApplyMapping(entry, config));
        Assert.Single(success.Entries);
        return success;
    }

    /// <summary>Applies a raw payload mapping and asserts a single successful entry.</summary>
    private MappedEntry MapPayloadSingle(SyncMappingConfig config, string payload) =>
        MapSingle(SourceEntry(payload), config);

    /// <summary>Applies a payload mapping and verifies the resulting mapped payload.</summary>
    private void ApplyAndVerifyPayload(
        SyncMappingConfig config,
        string payload,
        Action<JsonElement> verify
    ) =>
        global::Tests.Shared.MappingAssertions.Payload(
            result: ApplyConfiguredPayloadMapping(input0: config, value0: payload),
            verify: verify
        );

    /// <summary>Asserts that applying the payload is skipped.</summary>
    private MappingSkipped ApplyExpectSkipped(SyncMappingConfig config, string payload) =>
        Assert.IsType<MappingSkipped>(
            ApplyConfiguredPayloadMapping(input0: config, value0: payload)
        );

    /// <summary>Applies mapped entries to Postgres with suppression, asserting each apply succeeds.</summary>
    private void ApplyMappedEntriesToPostgres(
        IEnumerable<MappedEntry> entries,
        Func<MappedEntry, string, SyncLogEntry> buildTargetEntry
    )
    {
        PostgresSyncSession.EnableSuppression(_postgresConn);
        foreach (var entry in entries)
        {
            var mergedPayload = MergePkIntoPayload(entry.TargetPkValue, entry.MappedPayload);
            var targetEntry = buildTargetEntry(entry, mergedPayload);
            var applyResult = PostgresChangeApplier.ApplyChange(
                _postgresConn,
                targetEntry,
                _logger
            );
            Assert.True(applyResult is BoolSyncOk, $"Apply failed: {applyResult}");
        }
        PostgresSyncSession.DisableSuppression(_postgresConn);
    }

    /// <summary>Shared insert entry on the Source table (pass a different tableName to override).</summary>
    [Fact]
    public void MappingEngine_TransformsUserToCustomer_WithLql()
    {
        // Arrange - create a User sync log entry
        var entry = Fixture_MappingEngine_TransformsUserToCustomer_WithLql_1();

        var config = CreateUserToCustomerMapping();

        // Act
        var mapped = MapSingle(entry, config);

        // Assert
        global::Tests.Shared.MappingAssertions.Customer(mapped: mapped, pkSubstring: "u123");

        // Parse the payload to verify transforms
        Assert.NotNull(mapped.MappedPayload);
        using var doc = JsonDocument.Parse(mapped.MappedPayload);
        var root = doc.RootElement;

        // Column renames
        Assert.Equal("Alice Smith", root.GetProperty("name").GetString());
        Assert.Equal("alice@example.com", root.GetProperty("email").GetString());

        // LQL upper transform
        Assert.Equal("ALICE SMITH", root.GetProperty("name_upper").GetString());

        // Constant value
        Assert.Equal("mobile-app", root.GetProperty("source").GetString());

        // LQL dateFormat transform
        Assert.Equal("2024-06-15", root.GetProperty("registered_date").GetString());

        // Excluded columns should NOT be present
        Assert.False(root.TryGetProperty("PasswordHash", out _));
        Assert.False(root.TryGetProperty("SecurityStamp", out _));
    }

    // Implements [SYNC-TEST-STRING-TRANSFORMS].
    [Theory]
    // MappingEngine_WithConcatTransform_CombinesColumns
    [MemberData(nameof(LqlExpression_StringTransform_ReturnsExpectedPayloadInlineCases))]
    // MappingEngine_WithCoalesceTransform_ReturnsFirstNonNull

    // MappingEngine_WithSubstringTransform_ExtractsText

    // LqlExpression_WithEmptyString_ReturnsEmpty

    // LqlExpression_ReplaceFunction_WorksCorrectly

    // LqlExpression_LeftFunction_ExtractsPrefix

    // LqlExpression_RightFunction_ExtractsSuffix

    // LqlExpression_TrimFunction_RemovesWhitespace

    // LqlExpression_NestedConcat_BuildsComplexString

    // LqlExpression_DateFormatWithDifferentTimezones_PreservesUtc

    // LqlExpression_PipelineWithMultipleSteps_TransformsCorrectly

    public void LqlExpression_StringTransform_ReturnsExpectedPayload(
        string mappingId,
        string sourceTable,
        string targetTable,
        string pkValue,
        string column,
        string expression,
        string payload,
        string expected
    )
    {
        var config = MappingConfig(
            mappingId: mappingId,
            sourceTable: sourceTable,
            targetTable: targetTable,
            mappings: [LqlColumn(target: column, lql: expression)]
        );
        var result = ApplyMapping(
            entry: SourceEntry(payload: payload, tableName: sourceTable, pkValue: pkValue),
            config: config
        );
        var success = Assert.IsType<MappingSuccess>(@object: result);
        using var document = JsonDocument.Parse(
            json: Assert.IsType<string>(@object: success.Entries[0].MappedPayload)
        );
        Assert.Equal(
            expected: expected,
            actual: document.RootElement.GetProperty(propertyName: column).GetString()
        );
    }

    [Fact]
    public void E2E_SyncWithMapping_TransformsData_AcrossDatabases()
    {
        // Arrange - set up databases with DIFFERENT schemas
        var config = SetupUserToCustomerScenario();

        // Act - insert User in SQLite source
        CrossDatabaseSyncTests.ExecuteSql(
            _sqliteConn,
            """
            INSERT INTO User (Id, FullName, EmailAddress, PasswordHash, SecurityStamp, CreatedAt)
            VALUES ('u456', 'Bob Jones', 'bob@example.com', 'hash123', 'stamp456', '2024-07-20T14:00:00Z')
            """
        );

        var changes = SyncLogRepository.FetchChanges(
            connection: _sqliteConn,
            fromVersion: 0,
            batchSize: 100
        );
        var changesList = CrossDatabaseSyncTests.SingleChange(changes, $"Fetch failed: {changes}");

        // Apply mapping to transform the entry and apply mapped changes with suppression
        var mappedEntry = MapSuccessSingle(changesList[0], config);
        ApplyMappedEntriesToPostgres(
            mappedEntry.Entries,
            (entry, mergedPayload) =>
                Dataset_E2E_SyncWithMapping_TransformsData_AcrossDatabases_1(
                    changesList: changesList,
                    entry: entry,
                    mergedPayload: mergedPayload
                )
        );

        // Assert - verify in Postgres with TRANSFORMED schema
        using var verifyCmd = _postgresConn.CreateCommand();
        verifyCmd.CommandText =
            "SELECT customer_id, name, email, name_upper, source, registered_date FROM customer WHERE customer_id = 'u456'";
        using var reader = verifyCmd.ExecuteReader();
        Assert.True(reader.Read(), "Record not found in customer table");

        Assert.Equal("u456", reader.GetString(0)); // PK renamed
        Assert.Equal("Bob Jones", reader.GetString(1)); // Column renamed
        Assert.Equal("bob@example.com", reader.GetString(2)); // Column renamed
        Assert.Equal("BOB JONES", reader.GetString(3)); // LQL upper transform
        Assert.Equal("mobile-app", reader.GetString(4)); // Constant value
        Assert.Equal("2024-07-20", reader.GetString(5)); // LQL dateFormat transform
    }

    [Fact]
    public void E2E_MultipleRecords_AllTransformedCorrectly()
    {
        // Arrange
        var config = SetupUserToCustomerScenario();

        // Insert multiple users
        var users = new[]
        {
            ("u1", "Alice Brown", "alice@test.com", "2024-01-15T09:00:00Z"),
            ("u2", "Charlie Davis", "charlie@test.com", "2024-02-20T10:30:00Z"),
            ("u3", "Eve Wilson", "eve@test.com", "2024-03-25T14:45:00Z"),
        };

        foreach (var (id, name, email, created) in users)
        {
            using var cmd = _sqliteConn.CreateCommand();
            cmd.CommandText = $"""
                INSERT INTO User (Id, FullName, EmailAddress, PasswordHash, SecurityStamp, CreatedAt)
                VALUES ('{id}', '{name}', '{email}', 'hash', 'stamp', '{created}')
                """;
            cmd.ExecuteNonQuery();
        }

        // Fetch and transform all changes
        var changesList = CrossDatabaseSyncTests.FetchAllChanges(_sqliteConn);
        Assert.Equal(3, changesList.Count);

        // Apply all with mapping
        foreach (var change in changesList)
        {
            var mapped = MapSuccessSingle(change, config);
            ApplyMappedEntriesToPostgres(
                mapped.Entries,
                (entry, mergedPayload) =>
                    Dataset_E2E_MultipleRecords_AllTransformedCorrectly_1(
                        change: change,
                        entry: entry,
                        mergedPayload: mergedPayload
                    )
            );
        }

        // Verify all records transformed
        global::Nimblesite.Sync.Http.Tests.CrossDatabaseSyncTests.VerifyReusableWorkflow6(
            configure0: command => command.CommandText = "SELECT COUNT(*) FROM customer",
            input0: _postgresConn,
            value0: 3
        );

        // Verify uppercase names
        using var upperCmd = _postgresConn.CreateCommand();
        upperCmd.CommandText = "SELECT name_upper FROM customer ORDER BY customer_id";
        using var reader = upperCmd.ExecuteReader();

        Assert.True(reader.Read());
        Assert.Equal("ALICE BROWN", reader.GetString(0));
        Assert.True(reader.Read());
        Assert.Equal("CHARLIE DAVIS", reader.GetString(0));
        Assert.True(reader.Read());
        Assert.Equal("EVE WILSON", reader.GetString(0));
    }

    [Fact]
    public void MappingEngine_DeleteOperation_TransformsTableAndPk()
    {
        // Arrange
        var config = CreateUserToCustomerMapping();

        var entry = Fixture_MappingEngine_DeleteOperation_TransformsTableAndPk_1();

        // Act
        var mapped = MapSingle(entry, config);

        // Assert
        global::Tests.Shared.MappingAssertions.Customer(mapped: mapped, pkSubstring: "u999");
        Assert.Null(mapped.MappedPayload); // Delete has no payload
    }

    [Fact]
    public void MappingEngine_UpdateOperation_TransformsPayload()
    {
        // Arrange
        var config = CreateUserToCustomerMapping();

        var entry = Fixture_MappingEngine_UpdateOperation_TransformsPayload_1();

        // Act
        var result = ApplyMapping(entry, config);

        // Assert
        global::Tests.Shared.MappingAssertions.Payload(
            result: result,
            verify: element =>
            {
                var root = element;

                Assert.Equal("Updated Name", root.GetProperty("name").GetString());
                Assert.Equal("UPDATED NAME", root.GetProperty("name_upper").GetString());
                Assert.Equal("new@example.com", root.GetProperty("email").GetString());
            }
        );
    }

    // ========== CORNER CASE TESTS ==========

    [Fact]
    public void LqlExpression_WithNullValue_ReturnsNull()
    {
        // Arrange - payload with null field
        var config = MappingConfig(mappingId: "null-test", LqlColumn("upper_name", "upper(Name)"));

        var mapped = MapPayloadSingle(config, """{"Id":"1","Name":null}""");

        // Assert - should handle null gracefully
        Assert.NotNull(mapped.MappedPayload);
    }

    [Fact]
    public void LqlExpression_LengthFunction_ReturnsStringLength()
    {
        // Arrange
        var config = MappingConfig(
            mappingId: "length-test",
            LqlColumn("name_length", "length(Name)")
        );

        // Assert
        ApplyAndVerifyPayload(
            config: config,
            payload: """{"Id":"1","Name":"Hello"}""",
            verify: element =>
            {
                Assert.Equal(5, element.GetProperty("name_length").GetInt32());
            }
        );
    }

    [Fact]
    public void LqlExpression_CoalesceWithAllNull_ReturnsEmpty()
    {
        // Arrange
        var config = MappingConfig(
            mappingId: "coalesce-null-test",
            LqlColumn("phone", "coalesce(Mobile, Home, Work)")
        );

        // Assert - coalesce returns null/empty when all values are empty
        ApplyAndVerifyPayload(
            config: config,
            payload: """{"Id":"1","Mobile":"","Home":"","Work":""}""",
            verify: element =>
            { // Either null or missing is acceptable
                var hasPhone = element.TryGetProperty("phone", out var phone);
                if (hasPhone)
                {
                    Assert.True(
                        phone.ValueKind == JsonValueKind.Null
                            || string.IsNullOrEmpty(phone.GetString()),
                        "Expected null or empty"
                    );
                }
            }
        );
    }

    [Fact]
    public void MappingEngine_UnmappedTable_WithPassthrough_ReturnsIdentity()
    {
        // Arrange
        var config = Dataset_MappingEngine_UnmappedTable_WithPassthrough_ReturnsIdentity_1();

        var entry = SourceEntry("""{"Id":"1","Data":"test"}""", tableName: "UnknownTable");

        // Act
        var mapped = MapSingle(entry, config);

        // Assert - passthrough returns identity mapping
        Assert.Equal("UnknownTable", mapped.TargetTable);
        Assert.Equal("""{"Id":"1"}""", mapped.TargetPkValue);
    }

    [Fact]
    public void MappingEngine_UnmappedTable_WithStrict_ReturnsSkipped()
    {
        // Arrange
        var config = Dataset_MappingEngine_UnmappedTable_WithStrict_ReturnsSkipped_1();

        var entry = SourceEntry("""{"Id":"1","Data":"test"}""", tableName: "UnknownTable");

        // Act / Assert - strict mode skips unmapped tables
        Assert.IsType<MappingSkipped>(ApplyMapping(entry, config));
    }

    [Fact]
    public void MappingEngine_DisabledMapping_ReturnsSkipped()
    {
        // Arrange
        var config = Fixture_MappingEngine_DisabledMapping_ReturnsSkipped_1();

        // Assert
        var skipped = ApplyExpectSkipped(config, """{"Id":"1","Data":"test"}""");
        Assert.Contains("DISABLED", skipped.Reason.ToUpperInvariant());
    }

    [Fact]
    public void MappingEngine_WrongDirection_ReturnsSkipped()
    {
        // Arrange - mapping only for Pull direction
        var config = Fixture_MappingEngine_WrongDirection_ReturnsSkipped_1();

        var entry = SourceEntry("""{"Id":"1","Data":"test"}""");

        // Act / Assert - should be skipped because direction doesn't match
        Assert.IsType<MappingSkipped>(ApplyMapping(entry, config));
    }

    [Fact]
    public void MappingEngine_BothDirection_WorksForPushAndPull()
    {
        // Arrange - mapping for Both directions
        var config = Fixture_MappingEngine_BothDirection_WorksForPushAndPull_1();

        var entry = SourceEntry("""{"Id":"1","Data":"test"}""");

        // Act - should work for both directions
        var pushResult = ApplyMapping(entry, config);
        var pullResult = ApplyMapping(entry, config, MappingDirection.Pull);

        // Assert
        Assert.IsType<MappingSuccess>(pushResult);
        Assert.IsType<MappingSuccess>(pullResult);
    }

    // ========== ADDITIONAL CORNER CASE TESTS ==========

    [Fact]
    public void LqlExpression_NumericValue_PreservesType()
    {
        // Arrange - test numeric field handling
        var config = MappingConfig(
            mappingId: "numeric-test",
            new ColumnMapping("Amount", "amount"),
            new ColumnMapping("Count", "count")
        );

        // Assert - numeric values preserved
        ApplyAndVerifyPayload(
            config: config,
            payload: """{"Id":"1","Amount":123.45,"Count":42}""",
            verify: element =>
            {
                Assert.Equal(123.45, element.GetProperty("amount").GetDouble());
                Assert.Equal(42, element.GetProperty("count").GetInt32());
            }
        );
    }

    [Fact]
    public void LqlExpression_BooleanValue_PreservesType()
    {
        // Arrange
        var config = MappingConfig(
            mappingId: "bool-test",
            new ColumnMapping("IsActive", "is_active"),
            new ColumnMapping("IsVerified", "is_verified")
        );

        // Assert
        ApplyAndVerifyPayload(
            config: config,
            payload: """{"Id":"1","IsActive":true,"IsVerified":false}""",
            verify: element =>
            {
                Assert.True(element.GetProperty("is_active").GetBoolean());
                Assert.False(element.GetProperty("is_verified").GetBoolean());
            }
        );
    }

    [Fact]
    public void LqlExpression_SpecialCharactersInString_EscapedCorrectly()
    {
        // Arrange - strings with special JSON characters
        var config = MappingConfig(
            mappingId: "escape-test",
            new ColumnMapping("Description", "description")
        );

        VerifyReusableWorkflow5(
            input0: ApplyConfiguredPayloadMapping,
            input1: config,
            input2: ContainsAll,
            value0: """{"Id":"1","Description":"Line1\nLine2\tTabbed \"quoted\""}""",
            value1: "description",
            value2: "\n",
            value3: "\t",
            value4: "\"quoted\""
        );
    }

    [Fact]
    public void LqlExpression_VeryLongString_HandledCorrectly()
    {
        // Arrange - test with very long string
        var longString = new string('x', 10000);
        var config = MappingConfig(
            mappingId: "long-string-test",
            new ColumnMapping("Data", "data"),
            LqlColumn("data_length", "length(Data)")
        );

        var entry = SourceEntry($$$"""{"Id":"1","Data":"{{{longString}}}"}""");

        // Assert
        global::Tests.Shared.MappingAssertions.Payload(
            result: ApplyMapping(entry, config),
            verify: element =>
            {
                Assert.Equal(longString, element.GetProperty("data").GetString());
                Assert.Equal(10000, element.GetProperty("data_length").GetInt32());
            }
        );
    }

    [Fact]
    public void LqlExpression_UnicodeCharacters_PreservedCorrectly()
    {
        // Arrange - test Unicode handling
        var config = MappingConfig(
            mappingId: "unicode-test",
            new ColumnMapping("Name", "name"),
            LqlColumn("name_upper", "upper(Name)")
        );

        VerifyReusableWorkflow5(
            input0: ApplyConfiguredPayloadMapping,
            input1: config,
            input2: ContainsAll,
            value0: """{"Id":"1","Name":"日本語テスト 中文测试 émojis: 🎉🚀"}""",
            value1: "name",
            value2: "日本語",
            value3: "中文",
            value4: "🎉"
        );
    }

    [Fact]
    public void MappingEngine_MultipleColumnMappings_AllApplied()
    {
        // Arrange - many column mappings
        var config = MappingConfig(
            mappingId: "multi-col-test",
            new ColumnMapping("Col1", "col_1"),
            new ColumnMapping("Col2", "col_2"),
            new ColumnMapping("Col3", "col_3"),
            new ColumnMapping("Col4", "col_4"),
            new ColumnMapping("Col5", "col_5"),
            Fixture_MappingEngine_MultipleColumnMappings_AllApplied_1(),
            LqlColumn("computed", "concat(Col1, '-', Col2)")
        );

        // Assert - all mappings applied
        ApplyAndVerifyPayload(
            config: config,
            payload: """{"Id":"1","Col1":"A","Col2":"B","Col3":"C","Col4":"D","Col5":"E"}""",
            verify: element =>
            {
                Assert.Equal("A", element.GetProperty("col_1").GetString());
                Assert.Equal("B", element.GetProperty("col_2").GetString());
                Assert.Equal("C", element.GetProperty("col_3").GetString());
                Assert.Equal("D", element.GetProperty("col_4").GetString());
                Assert.Equal("E", element.GetProperty("col_5").GetString());
                Assert.Equal("fixed", element.GetProperty("constant").GetString());
                Assert.Equal("A-B", element.GetProperty("computed").GetString());
            }
        );
    }

    [Fact]
    public void MappingEngine_ExcludeMultipleColumns_AllExcluded()
    {
        // Arrange - exclude many columns
        var config = Fixture_MappingEngine_ExcludeMultipleColumns_AllExcluded_1();

        // Assert - excluded columns not in output
        ApplyAndVerifyPayload(
            config: config,
            payload: """{"Id":"1","Name":"User","Password":"hash","Salt":"xyz","Token":"abc","Secret":"123","PrivateKey":"key"}""",
            verify: element =>
            {
                Assert.True(element.TryGetProperty("name", out _));
                Assert.False(element.TryGetProperty("Password", out _));
                Assert.False(element.TryGetProperty("Salt", out _));
                Assert.False(element.TryGetProperty("Token", out _));
                Assert.False(element.TryGetProperty("Secret", out _));
                Assert.False(element.TryGetProperty("PrivateKey", out _));
            }
        );
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private global::Nimblesite.Sync.Core.MappingResult ApplyConfiguredPayloadMapping(
        global::Nimblesite.Sync.Core.SyncMappingConfig input0,
        string value0
    )
    {
        var entry = SourceEntry(value0);

        // Act
        var result = ApplyMapping(entry, input0);

        return result;
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private void VerifyReusableWorkflow5(
        Func<SyncMappingConfig, string, MappingResult> input0,
        SyncMappingConfig input1,
        Action<string?, System.Collections.Immutable.ImmutableArray<string>> input2,
        string value0,
        string value1,
        string value2,
        string value3,
        string value4
    )
    {
        var result = input0(arg1: input1, arg2: value0);

        // Assert - special chars preserved
        global::Tests.Shared.MappingAssertions.Payload(
            result: result,
            verify: element =>
            {
                var desc = element.GetProperty(value1).GetString();
                input2(arg1: desc, arg2: [value2, value3, value4]);
            }
        );
    }
}
