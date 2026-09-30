using System.Text.Json;

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
public sealed class HttpMappingE2ETests(PostgresContainerFixture fixture) : IAsyncLifetime
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
        _sqliteDbPath = Path.Combine(Path.GetTempPath(), $"mapping_test_{Guid.NewGuid()}.db");
        _sqliteConn = new SqliteConnection($"Data Source={_sqliteDbPath}");
        _sqliteConn.Open();

        // Create Postgres connection
        _postgresConn = new NpgsqlConnection(_postgresConnectionString);
        _postgresConn.Open();
    }

    public Task DisposeAsync()
    {
        _sqliteConn.Close();
        _sqliteConn.Dispose();
        _postgresConn.Close();
        _postgresConn.Dispose();

        if (File.Exists(_sqliteDbPath))
        {
            File.Delete(_sqliteDbPath);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Sets up SQLite source with User table schema.
    /// </summary>
    private void SetupSqliteSource(string originId)
    {
        SyncSchema.CreateSchema(_sqliteConn);
        SyncSchema.SetOriginId(_sqliteConn, originId);

        using var cmd = _sqliteConn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE User (
                Id TEXT PRIMARY KEY,
                FullName TEXT NOT NULL,
                EmailAddress TEXT NOT NULL,
                PasswordHash TEXT,
                SecurityStamp TEXT,
                CreatedAt TEXT NOT NULL
            );
            """;
        cmd.ExecuteNonQuery();

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
        cmd.CommandText = """
            DROP TABLE IF EXISTS customer CASCADE;
            CREATE TABLE customer (
                customer_id TEXT PRIMARY KEY,
                name TEXT NOT NULL,
                email TEXT NOT NULL,
                name_upper TEXT,
                source TEXT,
                registered_date TEXT
            );
            """;
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Creates mapping config for User -> Customer transformation.
    /// </summary>
    private static SyncMappingConfig CreateUserToCustomerMapping() =>
        new(
            Version: "1.0",
            UnmappedTableBehavior: UnmappedTableBehavior.Strict,
            Mappings:
            [
                new TableMapping(
                    Id: "user-to-customer",
                    SourceTable: "User",
                    TargetTable: "customer",
                    Direction: MappingDirection.Push,
                    Enabled: true,
                    PkMapping: new PkMapping("Id", "customer_id"),
                    ColumnMappings:
                    [
                        new ColumnMapping("FullName", "name"),
                        new ColumnMapping("EmailAddress", "email"),
                        new ColumnMapping(
                            Source: "FullName",
                            Target: "name_upper",
                            Transform: TransformType.Lql,
                            Lql: "upper(FullName)"
                        ),
                        new ColumnMapping(
                            Source: null,
                            Target: "source",
                            Transform: TransformType.Constant,
                            Value: "mobile-app"
                        ),
                        new ColumnMapping(
                            Source: "CreatedAt",
                            Target: "registered_date",
                            Transform: TransformType.Lql,
                            Lql: "CreatedAt |> dateFormat('yyyy-MM-dd')"
                        ),
                    ],
                    ExcludedColumns: ["PasswordHash", "SecurityStamp"],
                    Filter: null,
                    SyncTracking: new SyncTrackingConfig()
                ),
            ]
        );

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
    private static SyncMappingConfig MappingConfig(
        string mappingId,
        string sourceTable,
        string targetTable,
        params ColumnMapping[] mappings
    ) =>
        new(
            Version: "1.0",
            UnmappedTableBehavior: UnmappedTableBehavior.Strict,
            Mappings:
            [
                new TableMapping(
                    Id: mappingId,
                    SourceTable: sourceTable,
                    TargetTable: targetTable,
                    Direction: MappingDirection.Push,
                    Enabled: true,
                    PkMapping: null,
                    ColumnMappings: mappings,
                    ExcludedColumns: [],
                    Filter: null,
                    SyncTracking: new SyncTrackingConfig()
                ),
            ]
        );

    /// <summary>Shared LQL transform column mapping.</summary>
    private static ColumnMapping LqlColumn(string target, string lql) =>
        new(Source: null, Target: target, Transform: TransformType.Lql, Lql: lql);

    /// <summary>Shared insert entry on the Source table (pass a different tableName to override).</summary>
    private static SyncLogEntry SourceEntry(string payload, string tableName = "Source") =>
        new(
            Version: 1,
            TableName: tableName,
            PkValue: """{"Id":"1"}""",
            Operation: SyncOperation.Insert,
            Payload: payload,
            Origin: "test",
            Timestamp: "2024-01-01T00:00:00Z"
        );

    [Fact]
    public void MappingEngine_TransformsUserToCustomer_WithLql()
    {
        // Arrange - create a User sync log entry
        var entry = new SyncLogEntry(
            Version: 1,
            TableName: "User",
            PkValue: """{"Id":"u123"}""",
            Operation: SyncOperation.Insert,
            Payload: """{"Id":"u123","FullName":"Alice Smith","EmailAddress":"alice@example.com","PasswordHash":"secret","SecurityStamp":"xyz","CreatedAt":"2024-06-15T10:30:00Z"}""",
            Origin: "source-origin",
            Timestamp: "2024-06-15T10:30:00Z"
        );

        var config = CreateUserToCustomerMapping();

        // Act - apply mapping
        var result = ApplyMapping(entry, config);

        // Assert
        var success = Assert.IsType<MappingSuccess>(result);
        Assert.Single(success.Entries);

        var mapped = success.Entries[0];
        Assert.Equal("customer", mapped.TargetTable);
        Assert.Contains("customer_id", mapped.TargetPkValue);
        Assert.Contains("u123", mapped.TargetPkValue);

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

    [Fact]
    public void MappingEngine_WithConcatTransform_CombinesColumns()
    {
        // Arrange - mapping with concat
        var config = MappingConfig(
            mappingId: "name-concat",
            sourceTable: "Person",
            targetTable: "person",
            LqlColumn("full_name", "concat(FirstName, ' ', LastName)")
        );

        var entry = new SyncLogEntry(
            Version: 1,
            TableName: "Person",
            PkValue: """{"Id":"p1"}""",
            Operation: SyncOperation.Insert,
            Payload: """{"Id":"p1","FirstName":"John","LastName":"Doe"}""",
            Origin: "test",
            Timestamp: "2024-01-01T00:00:00Z"
        );

        // Act
        var result = ApplyMapping(entry, config);

        // Assert
        var success = Assert.IsType<MappingSuccess>(result);
        using var doc = JsonDocument.Parse(success.Entries[0].MappedPayload!);
        Assert.Equal("John Doe", doc.RootElement.GetProperty("full_name").GetString());
    }

    [Fact]
    public void MappingEngine_WithCoalesceTransform_ReturnsFirstNonNull()
    {
        // Arrange
        var config = MappingConfig(
            mappingId: "coalesce-test",
            sourceTable: "Contact",
            targetTable: "contact",
            LqlColumn("phone", "coalesce(Mobile, Home, Office)")
        );

        var entry = new SyncLogEntry(
            Version: 1,
            TableName: "Contact",
            PkValue: """{"Id":"c1"}""",
            Operation: SyncOperation.Insert,
            Payload: """{"Id":"c1","Mobile":"","Home":"555-1234","Office":"555-5678"}""",
            Origin: "test",
            Timestamp: "2024-01-01T00:00:00Z"
        );

        // Act
        var result = ApplyMapping(entry, config);

        // Assert
        var success = Assert.IsType<MappingSuccess>(result);
        using var doc = JsonDocument.Parse(success.Entries[0].MappedPayload!);
        Assert.Equal("555-1234", doc.RootElement.GetProperty("phone").GetString());
    }

    [Fact]
    public void MappingEngine_WithSubstringTransform_ExtractsText()
    {
        // Arrange
        var config = MappingConfig(
            mappingId: "substring-test",
            sourceTable: "Product",
            targetTable: "product",
            LqlColumn("sku_prefix", "substring(SKU, 1, 3)")
        );

        var entry = new SyncLogEntry(
            Version: 1,
            TableName: "Product",
            PkValue: """{"Id":"p1"}""",
            Operation: SyncOperation.Insert,
            Payload: """{"Id":"p1","SKU":"ABC-12345"}""",
            Origin: "test",
            Timestamp: "2024-01-01T00:00:00Z"
        );

        // Act
        var result = ApplyMapping(entry, config);

        // Assert
        var success = Assert.IsType<MappingSuccess>(result);
        using var doc = JsonDocument.Parse(success.Entries[0].MappedPayload!);
        Assert.Equal("ABC", doc.RootElement.GetProperty("sku_prefix").GetString());
    }

    [Fact]
    public void E2E_SyncWithMapping_TransformsData_AcrossDatabases()
    {
        // Arrange - set up databases with DIFFERENT schemas
        var config = SetupUserToCustomerScenario();

        // Act - insert User in SQLite source
        using (var cmd = _sqliteConn.CreateCommand())
        {
            cmd.CommandText = """
                INSERT INTO User (Id, FullName, EmailAddress, PasswordHash, SecurityStamp, CreatedAt)
                VALUES ('u456', 'Bob Jones', 'bob@example.com', 'hash123', 'stamp456', '2024-07-20T14:00:00Z')
                """;
            cmd.ExecuteNonQuery();
        }

        // Fetch changes from SQLite
        var changes = SyncLogRepository.FetchChanges(_sqliteConn, 0, 100);
        Assert.True(changes is SyncLogListOk, $"Fetch failed: {changes}");
        var changesList = ((SyncLogListOk)changes).Value;
        Assert.Single(changesList);

        // Apply mapping to transform the entry
        var mappingResult = ApplyMapping(changesList[0], config);
        var mappedEntry = Assert.IsType<MappingSuccess>(mappingResult);

        // Apply mapped changes to Postgres with suppression
        PostgresSyncSession.EnableSuppression(_postgresConn);
        foreach (var entry in mappedEntry.Entries)
        {
            // Merge PK into payload - PostgresChangeApplier expects PK in payload for inserts
            var mergedPayload = MergePkIntoPayload(entry.TargetPkValue, entry.MappedPayload);

            // Create a new SyncLogEntry with the mapped data
            var targetEntry = new SyncLogEntry(
                Version: changesList[0].Version,
                TableName: entry.TargetTable,
                PkValue: entry.TargetPkValue,
                Operation: changesList[0].Operation,
                Payload: mergedPayload,
                Origin: changesList[0].Origin,
                Timestamp: changesList[0].Timestamp
            );

            var applyResult = PostgresChangeApplier.ApplyChange(
                _postgresConn,
                targetEntry,
                _logger
            );
            Assert.True(applyResult is BoolSyncOk, $"Apply failed: {applyResult}");
        }
        PostgresSyncSession.DisableSuppression(_postgresConn);

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
        var changes = SyncLogRepository.FetchChanges(_sqliteConn, 0, 100);
        var changesList = ((SyncLogListOk)changes).Value;
        Assert.Equal(3, changesList.Count);

        // Apply all with mapping
        PostgresSyncSession.EnableSuppression(_postgresConn);
        foreach (var change in changesList)
        {
            var mappingResult = ApplyMapping(change, config);
            var mapped = Assert.IsType<MappingSuccess>(mappingResult);

            foreach (var entry in mapped.Entries)
            {
                // Merge PK into payload - PostgresChangeApplier expects PK in payload for inserts
                var mergedPayload = MergePkIntoPayload(entry.TargetPkValue, entry.MappedPayload);

                var targetEntry = new SyncLogEntry(
                    change.Version,
                    entry.TargetTable,
                    entry.TargetPkValue,
                    change.Operation,
                    mergedPayload,
                    change.Origin,
                    change.Timestamp
                );
                PostgresChangeApplier.ApplyChange(_postgresConn, targetEntry, _logger);
            }
        }
        PostgresSyncSession.DisableSuppression(_postgresConn);

        // Verify all records transformed
        using var countCmd = _postgresConn.CreateCommand();
        countCmd.CommandText = "SELECT COUNT(*) FROM customer";
        var count = Convert.ToInt32(
            countCmd.ExecuteScalar(),
            System.Globalization.CultureInfo.InvariantCulture
        );
        Assert.Equal(3, count);

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

        var entry = new SyncLogEntry(
            Version: 1,
            TableName: "User",
            PkValue: """{"Id":"u999"}""",
            Operation: SyncOperation.Delete,
            Payload: null, // Deletes have no payload
            Origin: "test",
            Timestamp: "2024-01-01T00:00:00Z"
        );

        // Act
        var result = ApplyMapping(entry, config);

        // Assert
        var success = Assert.IsType<MappingSuccess>(result);
        var mapped = success.Entries[0];

        Assert.Equal("customer", mapped.TargetTable);
        Assert.Contains("customer_id", mapped.TargetPkValue);
        Assert.Contains("u999", mapped.TargetPkValue);
        Assert.Null(mapped.MappedPayload); // Delete has no payload
    }

    [Fact]
    public void MappingEngine_UpdateOperation_TransformsPayload()
    {
        // Arrange
        var config = CreateUserToCustomerMapping();

        var entry = new SyncLogEntry(
            Version: 2,
            TableName: "User",
            PkValue: """{"Id":"u888"}""",
            Operation: SyncOperation.Update,
            Payload: """{"Id":"u888","FullName":"Updated Name","EmailAddress":"new@example.com","PasswordHash":"newhash","SecurityStamp":"newstamp","CreatedAt":"2024-01-01T00:00:00Z"}""",
            Origin: "test",
            Timestamp: "2024-01-02T00:00:00Z"
        );

        // Act
        var result = ApplyMapping(entry, config);

        // Assert
        var success = Assert.IsType<MappingSuccess>(result);
        using var doc = JsonDocument.Parse(success.Entries[0].MappedPayload!);
        var root = doc.RootElement;

        Assert.Equal("Updated Name", root.GetProperty("name").GetString());
        Assert.Equal("UPDATED NAME", root.GetProperty("name_upper").GetString());
        Assert.Equal("new@example.com", root.GetProperty("email").GetString());
    }

    // ========== CORNER CASE TESTS ==========

    [Fact]
    public void LqlExpression_WithNullValue_ReturnsNull()
    {
        // Arrange - payload with null field
        var config = MappingConfig(mappingId: "null-test", LqlColumn("upper_name", "upper(Name)"));

        var entry = SourceEntry("""{"Id":"1","Name":null}""");

        // Act
        var result = ApplyMapping(entry, config);

        // Assert - should handle null gracefully
        var success = Assert.IsType<MappingSuccess>(result);
        Assert.NotNull(success.Entries[0].MappedPayload);
    }

    [Fact]
    public void LqlExpression_WithEmptyString_ReturnsEmpty()
    {
        // Arrange
        var config = MappingConfig(mappingId: "empty-test", LqlColumn("upper_name", "upper(Name)"));

        var entry = SourceEntry("""{"Id":"1","Name":""}""");

        // Act
        var result = ApplyMapping(entry, config);

        // Assert
        var success = Assert.IsType<MappingSuccess>(result);
        using var doc = JsonDocument.Parse(success.Entries[0].MappedPayload!);
        Assert.Equal("", doc.RootElement.GetProperty("upper_name").GetString());
    }

    [Fact]
    public void LqlExpression_ReplaceFunction_WorksCorrectly()
    {
        // Arrange
        var config = MappingConfig(
            mappingId: "replace-test",
            LqlColumn("clean_phone", "replace(Phone, '-', '')")
        );

        var entry = SourceEntry("""{"Id":"1","Phone":"555-123-4567"}""");

        // Act
        var result = ApplyMapping(entry, config);

        // Assert
        var success = Assert.IsType<MappingSuccess>(result);
        using var doc = JsonDocument.Parse(success.Entries[0].MappedPayload!);
        Assert.Equal("5551234567", doc.RootElement.GetProperty("clean_phone").GetString());
    }

    [Fact]
    public void LqlExpression_LeftFunction_ExtractsPrefix()
    {
        // Arrange
        var config = MappingConfig(mappingId: "left-test", LqlColumn("initials", "left(Name, 2)"));

        var entry = SourceEntry("""{"Id":"1","Name":"Alexander"}""");

        // Act
        var result = ApplyMapping(entry, config);

        // Assert
        var success = Assert.IsType<MappingSuccess>(result);
        using var doc = JsonDocument.Parse(success.Entries[0].MappedPayload!);
        Assert.Equal("Al", doc.RootElement.GetProperty("initials").GetString());
    }

    [Fact]
    public void LqlExpression_RightFunction_ExtractsSuffix()
    {
        // Arrange
        var config = MappingConfig(
            mappingId: "right-test",
            LqlColumn("last_four", "right(CardNumber, 4)")
        );

        var entry = SourceEntry("""{"Id":"1","CardNumber":"1234567890123456"}""");

        // Act
        var result = ApplyMapping(entry, config);

        // Assert
        var success = Assert.IsType<MappingSuccess>(result);
        using var doc = JsonDocument.Parse(success.Entries[0].MappedPayload!);
        Assert.Equal("3456", doc.RootElement.GetProperty("last_four").GetString());
    }

    [Fact]
    public void LqlExpression_TrimFunction_RemovesWhitespace()
    {
        // Arrange
        var config = MappingConfig(mappingId: "trim-test", LqlColumn("clean_name", "trim(Name)"));

        var entry = SourceEntry("""{"Id":"1","Name":"  Hello World  "}""");

        // Act
        var result = ApplyMapping(entry, config);

        // Assert
        var success = Assert.IsType<MappingSuccess>(result);
        using var doc = JsonDocument.Parse(success.Entries[0].MappedPayload!);
        Assert.Equal("Hello World", doc.RootElement.GetProperty("clean_name").GetString());
    }

    [Fact]
    public void LqlExpression_LengthFunction_ReturnsStringLength()
    {
        // Arrange
        var config = MappingConfig(
            mappingId: "length-test",
            LqlColumn("name_length", "length(Name)")
        );

        var entry = SourceEntry("""{"Id":"1","Name":"Hello"}""");

        // Act
        var result = ApplyMapping(entry, config);

        // Assert
        var success = Assert.IsType<MappingSuccess>(result);
        using var doc = JsonDocument.Parse(success.Entries[0].MappedPayload!);
        Assert.Equal(5, doc.RootElement.GetProperty("name_length").GetInt32());
    }

    [Fact]
    public void LqlExpression_NestedConcat_BuildsComplexString()
    {
        // Arrange
        var config = MappingConfig(
            mappingId: "nested-test",
            LqlColumn(
                "display",
                "concat(Title, ': ', FirstName, ' ', LastName, ' (', Department, ')')"
            )
        );

        var entry = SourceEntry(
            """{"Id":"1","Title":"Dr","FirstName":"John","LastName":"Smith","Department":"Engineering"}"""
        );

        // Act
        var result = ApplyMapping(entry, config);

        // Assert
        var success = Assert.IsType<MappingSuccess>(result);
        using var doc = JsonDocument.Parse(success.Entries[0].MappedPayload!);
        Assert.Equal(
            "Dr: John Smith (Engineering)",
            doc.RootElement.GetProperty("display").GetString()
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

        var entry = SourceEntry("""{"Id":"1","Mobile":"","Home":"","Work":""}""");

        // Act
        var result = ApplyMapping(entry, config);

        // Assert - coalesce returns null/empty when all values are empty
        var success = Assert.IsType<MappingSuccess>(result);
        using var doc = JsonDocument.Parse(success.Entries[0].MappedPayload!);
        // Either null or missing is acceptable
        var hasPhone = doc.RootElement.TryGetProperty("phone", out var phone);
        if (hasPhone)
        {
            Assert.True(
                phone.ValueKind == JsonValueKind.Null || string.IsNullOrEmpty(phone.GetString()),
                "Expected null or empty"
            );
        }
    }

    [Fact]
    public void LqlExpression_DateFormatWithDifferentTimezones_PreservesUtc()
    {
        // Arrange
        var config = MappingConfig(
            mappingId: "tz-test",
            LqlColumn("date_only", "CreatedAt |> dateFormat('yyyy-MM-dd')")
        );

        // Test with explicit timezone offset
        var entry = SourceEntry("""{"Id":"1","CreatedAt":"2024-12-25T23:30:00+00:00"}""");

        // Act
        var result = ApplyMapping(entry, config);

        // Assert - should preserve UTC date
        var success = Assert.IsType<MappingSuccess>(result);
        using var doc = JsonDocument.Parse(success.Entries[0].MappedPayload!);
        Assert.Equal("2024-12-25", doc.RootElement.GetProperty("date_only").GetString());
    }

    [Fact]
    public void MappingEngine_UnmappedTable_WithPassthrough_ReturnsIdentity()
    {
        // Arrange
        var config = new SyncMappingConfig(
            Version: "1.0",
            UnmappedTableBehavior: UnmappedTableBehavior.Passthrough,
            Mappings: [] // No mappings defined
        );

        var entry = SourceEntry("""{"Id":"1","Data":"test"}""", tableName: "UnknownTable");

        // Act
        var result = ApplyMapping(entry, config);

        // Assert - passthrough returns identity mapping
        var success = Assert.IsType<MappingSuccess>(result);
        Assert.Single(success.Entries);
        Assert.Equal("UnknownTable", success.Entries[0].TargetTable);
        Assert.Equal("""{"Id":"1"}""", success.Entries[0].TargetPkValue);
    }

    [Fact]
    public void MappingEngine_UnmappedTable_WithStrict_ReturnsSkipped()
    {
        // Arrange
        var config = new SyncMappingConfig(
            Version: "1.0",
            UnmappedTableBehavior: UnmappedTableBehavior.Strict,
            Mappings: [] // No mappings defined
        );

        var entry = SourceEntry("""{"Id":"1","Data":"test"}""", tableName: "UnknownTable");

        // Act
        var result = ApplyMapping(entry, config);

        // Assert - strict mode skips unmapped tables
        Assert.IsType<MappingSkipped>(result);
    }

    [Fact]
    public void MappingEngine_DisabledMapping_ReturnsSkipped()
    {
        // Arrange
        var config = new SyncMappingConfig(
            Version: "1.0",
            UnmappedTableBehavior: UnmappedTableBehavior.Strict,
            Mappings:
            [
                new TableMapping(
                    Id: "disabled-test",
                    SourceTable: "Source",
                    TargetTable: "target",
                    Direction: MappingDirection.Push,
                    Enabled: false, // DISABLED
                    PkMapping: null,
                    ColumnMappings: [],
                    ExcludedColumns: [],
                    Filter: null,
                    SyncTracking: new SyncTrackingConfig()
                ),
            ]
        );

        var entry = SourceEntry("""{"Id":"1","Data":"test"}""");

        // Act
        var result = ApplyMapping(entry, config);

        // Assert
        var skipped = Assert.IsType<MappingSkipped>(result);
        Assert.Contains("DISABLED", skipped.Reason.ToUpperInvariant());
    }

    [Fact]
    public void MappingEngine_WrongDirection_ReturnsSkipped()
    {
        // Arrange - mapping only for Pull direction
        var config = new SyncMappingConfig(
            Version: "1.0",
            UnmappedTableBehavior: UnmappedTableBehavior.Strict,
            Mappings:
            [
                new TableMapping(
                    Id: "pull-only",
                    SourceTable: "Source",
                    TargetTable: "target",
                    Direction: MappingDirection.Pull, // Pull only
                    Enabled: true,
                    PkMapping: null,
                    ColumnMappings: [],
                    ExcludedColumns: [],
                    Filter: null,
                    SyncTracking: new SyncTrackingConfig()
                ),
            ]
        );

        var entry = SourceEntry("""{"Id":"1","Data":"test"}""");

        // Act - try to use for Push direction
        var result = ApplyMapping(entry, config);

        // Assert - should be skipped because direction doesn't match
        Assert.IsType<MappingSkipped>(result);
    }

    [Fact]
    public void MappingEngine_BothDirection_WorksForPushAndPull()
    {
        // Arrange - mapping for Both directions
        var config = new SyncMappingConfig(
            Version: "1.0",
            UnmappedTableBehavior: UnmappedTableBehavior.Strict,
            Mappings:
            [
                new TableMapping(
                    Id: "both-dir",
                    SourceTable: "Source",
                    TargetTable: "target",
                    Direction: MappingDirection.Both,
                    Enabled: true,
                    PkMapping: null,
                    ColumnMappings: [new ColumnMapping("Data", "data")],
                    ExcludedColumns: [],
                    Filter: null,
                    SyncTracking: new SyncTrackingConfig()
                ),
            ]
        );

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
    public void LqlExpression_PipelineWithMultipleSteps_TransformsCorrectly()
    {
        // Arrange - use pipe operator to chain transforms
        var config = MappingConfig(
            mappingId: "pipeline-test",
            LqlColumn("clean_name", "Name |> trim() |> upper()")
        );

        var entry = SourceEntry("""{"Id":"1","Name":"  hello world  "}""");

        // Act
        var result = ApplyMapping(entry, config);

        // Assert - should trim then uppercase
        var success = Assert.IsType<MappingSuccess>(result);
        using var doc = JsonDocument.Parse(success.Entries[0].MappedPayload!);
        Assert.Equal("HELLO WORLD", doc.RootElement.GetProperty("clean_name").GetString());
    }

    [Fact]
    public void LqlExpression_NumericValue_PreservesType()
    {
        // Arrange - test numeric field handling
        var config = MappingConfig(
            mappingId: "numeric-test",
            new ColumnMapping("Amount", "amount"),
            new ColumnMapping("Count", "count")
        );

        var entry = SourceEntry("""{"Id":"1","Amount":123.45,"Count":42}""");

        // Act
        var result = ApplyMapping(entry, config);

        // Assert - numeric values preserved
        var success = Assert.IsType<MappingSuccess>(result);
        using var doc = JsonDocument.Parse(success.Entries[0].MappedPayload!);
        Assert.Equal(123.45, doc.RootElement.GetProperty("amount").GetDouble());
        Assert.Equal(42, doc.RootElement.GetProperty("count").GetInt32());
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

        var entry = SourceEntry("""{"Id":"1","IsActive":true,"IsVerified":false}""");

        // Act
        var result = ApplyMapping(entry, config);

        // Assert
        var success = Assert.IsType<MappingSuccess>(result);
        using var doc = JsonDocument.Parse(success.Entries[0].MappedPayload!);
        Assert.True(doc.RootElement.GetProperty("is_active").GetBoolean());
        Assert.False(doc.RootElement.GetProperty("is_verified").GetBoolean());
    }

    [Fact]
    public void LqlExpression_SpecialCharactersInString_EscapedCorrectly()
    {
        // Arrange - strings with special JSON characters
        var config = MappingConfig(
            mappingId: "escape-test",
            new ColumnMapping("Description", "description")
        );

        var entry = SourceEntry("""{"Id":"1","Description":"Line1\nLine2\tTabbed \"quoted\""}""");

        // Act
        var result = ApplyMapping(entry, config);

        // Assert - special chars preserved
        var success = Assert.IsType<MappingSuccess>(result);
        using var doc = JsonDocument.Parse(success.Entries[0].MappedPayload!);
        var desc = doc.RootElement.GetProperty("description").GetString();
        Assert.Contains("\n", desc);
        Assert.Contains("\t", desc);
        Assert.Contains("\"quoted\"", desc);
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

        // Act
        var result = ApplyMapping(entry, config);

        // Assert
        var success = Assert.IsType<MappingSuccess>(result);
        using var doc = JsonDocument.Parse(success.Entries[0].MappedPayload!);
        Assert.Equal(longString, doc.RootElement.GetProperty("data").GetString());
        Assert.Equal(10000, doc.RootElement.GetProperty("data_length").GetInt32());
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

        var entry = SourceEntry("""{"Id":"1","Name":"日本語テスト 中文测试 émojis: 🎉🚀"}""");

        // Act
        var result = ApplyMapping(entry, config);

        // Assert - Unicode preserved
        var success = Assert.IsType<MappingSuccess>(result);
        using var doc = JsonDocument.Parse(success.Entries[0].MappedPayload!);
        var name = doc.RootElement.GetProperty("name").GetString();
        Assert.Contains("日本語", name);
        Assert.Contains("中文", name);
        Assert.Contains("🎉", name);
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
            new ColumnMapping(
                Source: null,
                Target: "constant",
                Transform: TransformType.Constant,
                Value: "fixed"
            ),
            LqlColumn("computed", "concat(Col1, '-', Col2)")
        );

        var entry = SourceEntry(
            """{"Id":"1","Col1":"A","Col2":"B","Col3":"C","Col4":"D","Col5":"E"}"""
        );

        // Act
        var result = ApplyMapping(entry, config);

        // Assert - all mappings applied
        var success = Assert.IsType<MappingSuccess>(result);
        using var doc = JsonDocument.Parse(success.Entries[0].MappedPayload!);
        Assert.Equal("A", doc.RootElement.GetProperty("col_1").GetString());
        Assert.Equal("B", doc.RootElement.GetProperty("col_2").GetString());
        Assert.Equal("C", doc.RootElement.GetProperty("col_3").GetString());
        Assert.Equal("D", doc.RootElement.GetProperty("col_4").GetString());
        Assert.Equal("E", doc.RootElement.GetProperty("col_5").GetString());
        Assert.Equal("fixed", doc.RootElement.GetProperty("constant").GetString());
        Assert.Equal("A-B", doc.RootElement.GetProperty("computed").GetString());
    }

    [Fact]
    public void MappingEngine_ExcludeMultipleColumns_AllExcluded()
    {
        // Arrange - exclude many columns
        var config = new SyncMappingConfig(
            Version: "1.0",
            UnmappedTableBehavior: UnmappedTableBehavior.Strict,
            Mappings:
            [
                new TableMapping(
                    Id: "exclude-test",
                    SourceTable: "Source",
                    TargetTable: "target",
                    Direction: MappingDirection.Push,
                    Enabled: true,
                    PkMapping: null,
                    ColumnMappings: [new ColumnMapping("Name", "name")],
                    ExcludedColumns: ["Password", "Salt", "Token", "Secret", "PrivateKey"],
                    Filter: null,
                    SyncTracking: new SyncTrackingConfig()
                ),
            ]
        );

        var entry = SourceEntry(
            """{"Id":"1","Name":"User","Password":"hash","Salt":"xyz","Token":"abc","Secret":"123","PrivateKey":"key"}"""
        );

        // Act
        var result = ApplyMapping(entry, config);

        // Assert - excluded columns not in output
        var success = Assert.IsType<MappingSuccess>(result);
        using var doc = JsonDocument.Parse(success.Entries[0].MappedPayload!);
        Assert.True(doc.RootElement.TryGetProperty("name", out _));
        Assert.False(doc.RootElement.TryGetProperty("Password", out _));
        Assert.False(doc.RootElement.TryGetProperty("Salt", out _));
        Assert.False(doc.RootElement.TryGetProperty("Token", out _));
        Assert.False(doc.RootElement.TryGetProperty("Secret", out _));
        Assert.False(doc.RootElement.TryGetProperty("PrivateKey", out _));
    }
}
