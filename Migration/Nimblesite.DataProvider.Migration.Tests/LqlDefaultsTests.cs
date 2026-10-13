// Implements [CI-TEST-SHARED-COMMAND].

using static global::Tests.Shared.ResultAssertions;
using static global::Tests.Shared.StringAssertions;

namespace Nimblesite.DataProvider.Migration.Tests;

/// <summary>
/// E2E tests proving LQL (Language Query Language) default values are TRULY platform-independent.
/// Same LQL expression produces correct, equivalent behavior on both SQLite AND PostgreSQL.
/// These tests verify that:
/// 1. The same schema definition works on both platforms
/// 2. Default values are properly applied when inserting without explicit values
/// 3. The resulting data is semantically equivalent across platforms
/// Uses the shared postgres container; each test gets its own database.
/// </summary>
[Collection(PostgresTestSuite.Name)]
public sealed partial class LqlDefaultsTests(PostgresContainerFixture fixture) : IAsyncLifetime
{
    private NpgsqlConnection _pgConnection = null!;
    private SqliteConnection _sqliteConnection = null!;
    private string _sqliteDbPath = null!;
    private readonly ILogger _logger = NullLogger.Instance;

    public async Task InitializeAsync()
    {
        _pgConnection = await fixture.CreateDatabaseAsync("lql_test").ConfigureAwait(false);

        // Setup SQLite with file-based database
        _sqliteDbPath = Path.Combine(Path.GetTempPath(), $"lql_defaults_{Guid.NewGuid():N}.db");
        _sqliteConnection = new SqliteConnection($"Data Source={_sqliteDbPath}");
        await _sqliteConnection.OpenAsync().ConfigureAwait(false);
    }

    public async Task DisposeAsync()
    {
        await _pgConnection.DisposeAsync().ConfigureAwait(false);
        _sqliteConnection.Dispose();
        if (File.Exists(_sqliteDbPath))
        {
            try
            {
                File.Delete(_sqliteDbPath);
            }
            catch (IOException)
            { /* File may be locked */
            }
            catch (UnauthorizedAccessException)
            { /* May not have permission */
            }
        }
    }

    // =========================================================================
    // BOOLEAN DEFAULTS - true/false across platforms
    // =========================================================================

    // Implements [CI-TEST-SHARED-CASE-MATRIX].
    [Theory]
    [MemberData(nameof(LqlBoolean_WorksOnBothPlatformsCases))]
    public void LqlBoolean_WorksOnBothPlatforms(
        Func<SchemaDefinition> input0,
        string table,
        string column,
        string[] pgExpected,
        string[] sqliteExpected,
        bool pgValue,
        long sqliteValue
    )
    {
        var (pgDdl, sqliteDdl) = ReadBothDdl(input0: input0, tableName: table);
        ContainsAll(actual: pgDdl, expected: [.. pgExpected]);
        ContainsAll(actual: sqliteDdl, expected: [.. sqliteExpected]);
        AssertBothColumnCount(tableName: table, expected: 2);
        ExecutePg($"INSERT INTO {table} (id) VALUES (1)");
        ExecuteSqlite($"INSERT INTO {table} (id) VALUES (1)");
        AssertPgSqlite<bool, long>(
            pgValue,
            $"SELECT {column} FROM {table} WHERE id = 1",
            sqliteValue,
            $"SELECT {column} FROM {table} WHERE id = 1"
        );
    }

    // =========================================================================
    // TIMESTAMP DEFAULTS - now() and current_timestamp()
    // =========================================================================

    [Fact]
    public void LqlNow_DefaultsToCurrentTime_BothPlatforms()
    {
        // Arrange
        var schema = Fixture_LqlNow_DefaultsToCurrentTime_BothPlatforms_1();

        var beforeTest = DateTime.UtcNow.AddSeconds(-1);

        // Act
        var (pgDdl, sqliteDdl) = VerifySharedContinuation6(
            input0: _pgConnection,
            input1: schema,
            input2: _sqliteConnection,
            value0: "events",
            value1: "events"
        );

        // PostgreSQL: now() translates to CURRENT_TIMESTAMP
        Assert.Contains("created_at", pgDdl);
        Assert.Contains("CURRENT_TIMESTAMP", pgDdl.ToUpperInvariant());

        // SQLite: now() translates to (datetime('now'))
        Assert.Contains("[created_at]", sqliteDdl);
        Assert.Contains("datetime('now')", sqliteDdl.ToLowerInvariant());

        // VERIFY COLUMNS: Exactly 2 columns
        Assert.Equal(2, GetPostgresColumns(_pgConnection, "events").Count);
        Assert.Equal(2, GetSqliteColumns(_sqliteConnection, "events").Count);

        ExecutePg("INSERT INTO events (id) VALUES (1)");
        ExecuteSqlite("INSERT INTO events (id) VALUES (1)");

        var afterTest = DateTime.UtcNow.AddSeconds(1);

        // VERIFY RUNTIME: Both should have timestamps close to now
        var pgValue = QueryPg<DateTime>("SELECT created_at FROM events WHERE id = 1");
        var sqliteValue = QuerySqlite<string>("SELECT created_at FROM events WHERE id = 1");

        // Postgres: DateTime value
        Assert.InRange(pgValue, beforeTest, afterTest);

        // SQLite: String in ISO format (e.g., "2025-01-15 10:30:45")
        Assert.True(DateTime.TryParse(sqliteValue, out var sqliteDt));
        // SQLite CURRENT_TIMESTAMP is in UTC
        Assert.InRange(sqliteDt, beforeTest.AddHours(-24), afterTest.AddHours(24));
    }

    [Fact]
    public void LqlCurrentTimestamp_SameAsNow_BothPlatforms()
    {
        // Arrange
        var (pgValue, sqliteValue) = VerifyReusableWorkflow23(
            input0: VerifySharedChecks3,
            input1: Fixture_LqlCurrentTimestamp_SameAsNow_BothPlatforms_1,
            value0: "INSERT INTO logs (id) VALUES (1)",
            value1: "INSERT INTO logs (id) VALUES (1)",
            value2: "SELECT timestamp FROM logs WHERE id = 1",
            value3: "SELECT timestamp FROM logs WHERE id = 1"
        );

        Assert.True(pgValue > DateTime.MinValue);
        Assert.False(string.IsNullOrEmpty(sqliteValue));
    }

    [Fact]
    public void LqlCurrentDate_ReturnsDateOnly_BothPlatforms()
    {
        // Arrange
        var (pgValue, sqliteValue) = VerifyReusableWorkflow23(
            input0: VerifySharedChecks3,
            input1: Fixture_LqlCurrentDate_ReturnsDateOnly_BothPlatforms_1,
            value0: "INSERT INTO daily_records (id) VALUES (1)",
            value1: "INSERT INTO daily_records (id) VALUES (1)",
            value2: "SELECT record_date FROM daily_records WHERE id = 1",
            value3: "SELECT record_date FROM daily_records WHERE id = 1"
        );

        // Postgres: Date value
        Assert.Equal(DateTime.UtcNow.Date, pgValue.Date);

        // SQLite: Date string (e.g., "2025-01-15")
        Assert.True(DateTime.TryParse(sqliteValue, out var sqliteDt));
    }

    // =========================================================================
    // NUMERIC DEFAULTS - integers and decimals
    // =========================================================================

    // Implements [CI-TEST-SHARED-CASE-MATRIX].
    [Theory]
    [MemberData(nameof(LqlNumeric_SameValueBothPlatformsCases))]
    public void LqlNumeric_SameValueBothPlatforms_ContextCases(
        string caseName,
        Func<SchemaDefinition> input0,
        string insertSql,
        string table,
        string[] columns,
        long[] expected
    )
    {
        Assert.False(condition: string.IsNullOrEmpty(value: caseName));
        VerifySharedChecks3(input0: input0, value0: insertSql, value1: insertSql);
        for (var i = 0; i < columns.Length; i++)
        {
            AssertPgSqliteLong(
                expected: expected[i],
                pgSql: $"SELECT {columns[i]} FROM {table} WHERE id = 1",
                sqliteSql: $"SELECT {columns[i]} FROM {table} WHERE id = 1"
            );
        }
    }

    [Fact]
    public void LqlNumericDecimal_DefaultsCorrectly_BothPlatforms()
    {
        // Arrange
        VerifySharedChecks3(
            input0: Fixture_LqlNumericDecimal_DefaultsCorrectly_BothPlatforms_1,
            value0: "INSERT INTO prices (id) VALUES (1)",
            value1: "INSERT INTO prices (id) VALUES (1)"
        );

        // Assert - Same decimal values (within floating-point tolerance for SQLite)
        VerifyReusableWorkflow4(
            value0: 99.99m,
            value1: "SELECT amount FROM prices WHERE id = 1",
            value2: 99.99,
            value3: "SELECT amount FROM prices WHERE id = 1",
            value4: 2
        );

        VerifyReusableWorkflow4(
            value0: 0.0825m,
            value1: "SELECT tax_rate FROM prices WHERE id = 1",
            value2: 0.0825,
            value3: "SELECT tax_rate FROM prices WHERE id = 1",
            value4: 4
        );

        Assert.Equal(-10.50m, QueryPg<decimal>("SELECT discount FROM prices WHERE id = 1"));
        Assert.Equal(-10.50, QuerySqlite<double>("SELECT discount FROM prices WHERE id = 1"), 2);
    }

    // =========================================================================
    // STRING DEFAULTS - quoted strings
    // =========================================================================

    [Fact]
    public void LqlStringLiteral_DefaultsCorrectly_BothPlatforms()
    {
        // Arrange - Strings must be quoted with single quotes in LQL
        VerifySharedContinuation2(
            input0: Fixture_LqlStringLiteral_DefaultsCorrectly_BothPlatforms_1,
            value0: "INSERT INTO statuses (id) VALUES (1)",
            value1: "INSERT INTO statuses (id) VALUES (1)"
        );

        // Assert - Same string values on both platforms
        VerifyCompletionWorkflow1(
            input0: AssertDatabaseQueryValues,
            value0: "active",
            value1: "SELECT status FROM statuses WHERE id = 1",
            value2: "active",
            value3: "SELECT status FROM statuses WHERE id = 1",
            value4: "uncategorized",
            value5: "SELECT category FROM statuses WHERE id = 1",
            value6: "uncategorized",
            value7: "SELECT category FROM statuses WHERE id = 1"
        );

        Assert.Equal("", QueryPg<string>("SELECT empty FROM statuses WHERE id = 1"));
        AssertPgSqlite<string, string>(
            "",
            "SELECT empty FROM statuses WHERE id = 1",
            "",
            "SELECT empty FROM statuses WHERE id = 1"
        );
    }

    // =========================================================================
    // UUID DEFAULTS - gen_uuid()
    // =========================================================================

    [Fact]
    public void LqlGenUuid_GeneratesValidUuid_BothPlatforms()
    {
        // Arrange
        var (pgDdl, sqliteDdl) = VerifySharedPhase4(
            input0: Fixture_LqlGenUuid_GeneratesValidUuid_BothPlatforms_1,
            input1: _pgConnection,
            input2: _sqliteConnection,
            value0: "entities",
            value1: "entities"
        );

        // PostgreSQL: gen_uuid() translates to gen_random_uuid()
        Assert.Contains("id", pgDdl);
        Assert.Contains("gen_random_uuid()", pgDdl.ToLowerInvariant());

        // SQLite: gen_uuid() translates to complex hex expression
        Assert.Contains("[id]", sqliteDdl);
        Assert.Contains("hex(randomblob", sqliteDdl.ToLowerInvariant());

        // VERIFY COLUMNS: Exactly 2 columns
        var pgCols = GetPostgresColumns(_pgConnection, "entities");
        VerifySharedPhase7(input0: pgCols, value0: 2, value2: "name");

        var sqliteCols = GetSqliteColumns(_sqliteConnection, "entities");
        VerifySharedPhase7(input0: sqliteCols, value0: 2, value2: "name");

        // Insert without specifying UUID (let default generate it)
        ExecutePg("INSERT INTO entities (name) VALUES ('test1')");
        ExecutePg("INSERT INTO entities (name) VALUES ('test2')");
        ExecuteSqlite("INSERT INTO entities (name) VALUES ('test1')");
        ExecuteSqlite("INSERT INTO entities (name) VALUES ('test2')");

        // VERIFY RUNTIME: Should generate valid UUIDs
        var pgUuid1 = QueryPg<Guid>("SELECT id FROM entities WHERE name = 'test1'");
        var pgUuid2 = QueryPg<Guid>("SELECT id FROM entities WHERE name = 'test2'");

        Assert.NotEqual(Guid.Empty, pgUuid1);
        Assert.NotEqual(Guid.Empty, pgUuid2);
        Assert.NotEqual(pgUuid1, pgUuid2); // Unique UUIDs

        var sqliteUuid1 = QuerySqlite<string>("SELECT id FROM entities WHERE name = 'test1'");
        var sqliteUuid2 = QuerySqlite<string>("SELECT id FROM entities WHERE name = 'test2'");

        // SQLite stores UUIDs as text - verify they're valid UUID format
        Assert.True(Guid.TryParse(sqliteUuid1, out var parsed1));
        Assert.True(Guid.TryParse(sqliteUuid2, out var parsed2));
        Assert.NotEqual(Guid.Empty, parsed1);
        Assert.NotEqual(Guid.Empty, parsed2);
        Assert.NotEqual(parsed1, parsed2); // Unique UUIDs
    }

    [Fact]
    public void LqlUuidAlias_SameAsGenUuid_BothPlatforms()
    {
        // Arrange - uuid() should work the same as gen_uuid()
        VerifySharedContinuation2(
            input0: Fixture_LqlUuidAlias_SameAsGenUuid_BothPlatforms_1,
            value0: "INSERT INTO items (label) VALUES ('item1')",
            value1: "INSERT INTO items (label) VALUES ('item1')"
        );

        // Assert
        var pgUuid = QueryPg<Guid>("SELECT id FROM items WHERE label = 'item1'");
        Assert.NotEqual(Guid.Empty, pgUuid);

        var sqliteUuid = QuerySqlite<string>("SELECT id FROM items WHERE label = 'item1'");
        Assert.True(Guid.TryParse(sqliteUuid, out var parsed));
        Assert.NotEqual(Guid.Empty, parsed);
    }

    // =========================================================================
    // COMBINED DEFAULTS - Multiple LQL defaults in one table
    // =========================================================================

    [Fact]
    public void LqlMultipleDefaults_AllWorkTogether_BothPlatforms()
    {
        // Arrange - Real-world scenario with multiple LQL defaults
        var (pgDdl, sqliteDdl) = VerifySharedPhase4(
            input0: Fixture_LqlMultipleDefaults_AllWorkTogether_BothPlatforms_1,
            input1: _pgConnection,
            input2: _sqliteConnection,
            value0: "audit_records",
            value1: "audit_records"
        );

        // PostgreSQL DDL verification
        Assert.Contains("gen_random_uuid()", pgDdl.ToLowerInvariant()); // UUID
        ContainsAll(
            actual: pgDdl,
            expected: ["DEFAULT true", "DEFAULT 0", "DEFAULT 5.0", "DEFAULT 'pending'"]
        ); // string
        Assert.Contains("CURRENT_TIMESTAMP", pgDdl.ToUpperInvariant()); // now()

        // SQLite DDL verification
        Assert.Contains("hex(randomblob", sqliteDdl.ToLowerInvariant()); // UUID
        ContainsAll(
            actual: sqliteDdl,
            expected: ["DEFAULT 1", "DEFAULT 0", "DEFAULT 5.0", "DEFAULT 'pending'"]
        ); // string
        Assert.Contains("datetime('now')", sqliteDdl.ToLowerInvariant()); // now()

        // VERIFY COLUMNS: Exactly 7 columns with correct names
        var pgCols = GetPostgresColumns(_pgConnection, "audit_records");
        VerifySharedPhase7(input0: pgCols, value0: 7, value2: "action");
        Assert.Contains("is_success", pgCols);
        Assert.Contains("retry_count", pgCols);
        Assert.Contains("priority", pgCols);
        Assert.Contains("status", pgCols);
        Assert.Contains("created_at", pgCols);

        var sqliteCols = GetSqliteColumns(_sqliteConnection, "audit_records");
        Assert.Equal(7, sqliteCols.Count);

        // Insert with only required fields - all defaults should apply
        ExecutePg("INSERT INTO audit_records (action) VALUES ('user_login')");
        ExecuteSqlite("INSERT INTO audit_records (action) VALUES ('user_login')");

        // VERIFY RUNTIME: All defaults applied on both platforms
        // Postgres
        var pgId = QueryPg<Guid>("SELECT id FROM audit_records WHERE action = 'user_login'");
        Assert.NotEqual(Guid.Empty, pgId);
        Assert.True(
            QueryPg<bool>("SELECT is_success FROM audit_records WHERE action = 'user_login'")
        );
        Assert.Equal(
            0,
            QueryPg<int>("SELECT retry_count FROM audit_records WHERE action = 'user_login'")
        );
        Assert.Equal(
            5.0m,
            QueryPg<decimal>("SELECT priority FROM audit_records WHERE action = 'user_login'")
        );
        Assert.Equal(
            "pending",
            QueryPg<string>("SELECT status FROM audit_records WHERE action = 'user_login'")
        );
        Assert.True(
            QueryPg<DateTime>("SELECT created_at FROM audit_records WHERE action = 'user_login'")
                > DateTime.MinValue
        );

        // SQLite
        var sqliteId = QuerySqlite<string>(
            "SELECT id FROM audit_records WHERE action = 'user_login'"
        );
        Assert.True(Guid.TryParse(sqliteId, out _));
        Assert.Equal(
            1,
            QuerySqlite<long>("SELECT is_success FROM audit_records WHERE action = 'user_login'")
        );
        Assert.Equal(
            0,
            QuerySqlite<long>("SELECT retry_count FROM audit_records WHERE action = 'user_login'")
        );
        Assert.Equal(
            5.0,
            QuerySqlite<double>("SELECT priority FROM audit_records WHERE action = 'user_login'"),
            1
        );
        Assert.Equal(
            "pending",
            QuerySqlite<string>("SELECT status FROM audit_records WHERE action = 'user_login'")
        );
        Assert.False(
            string.IsNullOrEmpty(
                QuerySqlite<string>(
                    "SELECT created_at FROM audit_records WHERE action = 'user_login'"
                )
            )
        );
    }

    // =========================================================================
    // IDEMPOTENCY - Schema can be applied multiple times with SAME RESULT
    // =========================================================================

    [Fact]
    public void LqlDefaults_Idempotent_BothPlatforms()
    {
        // Arrange
        var schema = Fixture_LqlDefaults_Idempotent_BothPlatforms_1();

        // Act - Apply FIRST time
        var (pgDdlBefore, sqliteDdlBefore) = VerifySharedContinuation6(
            input0: _pgConnection,
            input1: schema,
            input2: _sqliteConnection,
            value0: "rerunnable",
            value1: "rerunnable"
        );

        // Apply SECOND time
        var (pgDdlAfter, sqliteDdlAfter) = VerifySharedContinuation6(
            input0: _pgConnection,
            input1: schema,
            input2: _sqliteConnection,
            value0: "rerunnable",
            value1: "rerunnable"
        );

        // ASSERT 1: DDL is IDENTICAL before and after (proves true idempotency)
        Assert.Equal(pgDdlBefore, pgDdlAfter);
        Assert.Equal(sqliteDdlBefore, sqliteDdlAfter);

        // ASSERT 2: Table structure is EXACTLY what we defined
        // PostgreSQL: Verify table exists with correct columns
        Assert.True(SchemaVerifier.PostgresTableExists(_pgConnection, "rerunnable", "public"));
        var pgColumns = GetPostgresColumns(_pgConnection, "rerunnable", "public");
        VerifySharedPhase7(input0: pgColumns, value0: 3, value2: "active");
        Assert.Contains("count", pgColumns);

        // SQLite: Verify table exists with correct columns
        Assert.True(SchemaVerifier.SqliteTableExists(_sqliteConnection, "rerunnable"));
        var sqliteColumns = GetSqliteColumns(_sqliteConnection, "rerunnable");
        VerifySharedPhase7(input0: sqliteColumns, value0: 3, value2: "active");
        Assert.Contains("count", sqliteColumns);

        // ASSERT 3: Defaults are CORRECT in DDL
        // PostgreSQL: Check defaults in DDL
        ContainsAll(actual: pgDdlAfter, expected: ["DEFAULT true", "DEFAULT 1"]); // integer 1

        // SQLite: Check defaults in DDL
        Assert.Contains("DEFAULT 1", sqliteDdlAfter); // boolean true = 1, integer 1 = 1

        // ASSERT 4: Primary key is correctly defined
        Assert.Contains("PRIMARY KEY", pgDdlAfter.ToUpperInvariant());
        Assert.Contains("PRIMARY KEY", sqliteDdlAfter.ToUpperInvariant());

        // ASSERT 5: Runtime defaults work correctly
        ExecutePg("INSERT INTO rerunnable (id) VALUES (1)");
        ExecuteSqlite("INSERT INTO rerunnable (id) VALUES (1)");

        Assert.True(QueryPg<bool>("SELECT active FROM rerunnable WHERE id = 1"));
        Assert.Equal(1, QueryPg<int>("SELECT count FROM rerunnable WHERE id = 1"));

        Assert.Equal(1, QuerySqlite<long>("SELECT active FROM rerunnable WHERE id = 1"));
        Assert.Equal(1, QuerySqlite<long>("SELECT count FROM rerunnable WHERE id = 1"));

        // ASSERT 6: Only ONE row exists (no duplicate inserts from idempotent migrations)
        Assert.Equal(1L, QueryPg<long>("SELECT COUNT(*) FROM rerunnable"));
        Assert.Equal(1L, QuerySqlite<long>("SELECT COUNT(*) FROM rerunnable"));
    }

    // =========================================================================
    // EDGE CASES - Corner cases proving true platform independence
    // =========================================================================

    [Fact]
    public void LqlNumeric_VerySmallDecimal_SameValueBothPlatforms()
    {
        // Arrange - Test precision with very small decimal values
        VerifySharedContinuation2(
            input0: Fixture_LqlNumeric_VerySmallDecimal_SameValueBothPlatforms_1,
            value0: "INSERT INTO precision_test (id) VALUES (1)",
            value1: "INSERT INTO precision_test (id) VALUES (1)"
        );

        // Assert
        VerifyReusableWorkflow4(
            value0: 0.00000001m,
            value1: "SELECT tiny FROM precision_test WHERE id = 1",
            value2: 0.00000001,
            value3: "SELECT tiny FROM precision_test WHERE id = 1",
            value4: 8
        );

        VerifyReusableWorkflow4(
            value0: 3.1415926535m,
            value1: "SELECT scientific FROM precision_test WHERE id = 1",
            value2: 3.1415926535,
            value3: "SELECT scientific FROM precision_test WHERE id = 1",
            value4: 10
        );
    }

    [Fact]
    public void LqlString_SpecialCharacters_SameValueBothPlatforms()
    {
        // Arrange - Test strings with special characters (escaped in LQL as SQL single quotes)
        VerifySharedContinuation2(
            input0: Fixture_LqlString_SpecialCharacters_SameValueBothPlatforms_1,
            value0: "INSERT INTO special_strings (id) VALUES (1)",
            value1: "INSERT INTO special_strings (id) VALUES (1)"
        );

        // Assert - Same string values on both platforms
        VerifyCompletionWorkflow1(
            input0: AssertDatabaseQueryValues,
            value0: "hello world",
            value1: "SELECT with_spaces FROM special_strings WHERE id = 1",
            value2: "hello world",
            value3: "SELECT with_spaces FROM special_strings WHERE id = 1",
            value4: "test123",
            value5: "SELECT with_numbers FROM special_strings WHERE id = 1",
            value6: "test123",
            value7: "SELECT with_numbers FROM special_strings WHERE id = 1"
        );

        VerifyCompletionWorkflow1(
            input0: AssertDatabaseQueryValues,
            value0: "foo-bar-baz",
            value1: "SELECT with_hyphen FROM special_strings WHERE id = 1",
            value2: "foo-bar-baz",
            value3: "SELECT with_hyphen FROM special_strings WHERE id = 1",
            value4: "snake_case_value",
            value5: "SELECT with_underscore FROM special_strings WHERE id = 1",
            value6: "snake_case_value",
            value7: "SELECT with_underscore FROM special_strings WHERE id = 1"
        );
    }

    [Fact]
    public void LqlBoolean_MultipleColumns_AllDefaultCorrectly()
    {
        // Arrange - Test multiple boolean defaults in various combinations
        VerifySharedContinuation2(
            input0: Fixture_LqlBoolean_MultipleColumns_AllDefaultCorrectly_1,
            value0: "INSERT INTO feature_flags (id) VALUES (1)",
            value1: "INSERT INTO feature_flags (id) VALUES (1)"
        );

        // Assert - All flags match expected values
        VerifyCompletionWorkflow2(
            value1: "SELECT flag_a FROM feature_flags WHERE id = 1",
            value3: "SELECT flag_a FROM feature_flags WHERE id = 1",
            value5: "SELECT flag_b FROM feature_flags WHERE id = 1",
            value7: "SELECT flag_b FROM feature_flags WHERE id = 1"
        );
        VerifyCompletionWorkflow2(
            value1: "SELECT flag_c FROM feature_flags WHERE id = 1",
            value3: "SELECT flag_c FROM feature_flags WHERE id = 1",
            value5: "SELECT flag_d FROM feature_flags WHERE id = 1",
            value7: "SELECT flag_d FROM feature_flags WHERE id = 1"
        );
    }

    [Fact]
    public void LqlUuid_MultipleInserts_AllUnique_BothPlatforms()
    {
        // Arrange - Verify UUID generation is truly unique across many inserts
        var schema = Fixture_LqlUuid_MultipleInserts_AllUnique_BothPlatforms_1();

        // Act
        ApplySchema(_pgConnection, schema, PostgresDdlGenerator.Generate);
        ApplySchema(_sqliteConnection, schema, SqliteDdlGenerator.Generate);

        // Insert 10 rows in each database
        for (var i = 1; i <= 10; i++)
        {
            ExecutePg($"INSERT INTO uuid_test (seq) VALUES ({i})");
            ExecuteSqlite($"INSERT INTO uuid_test (seq) VALUES ({i})");
        }

        // Assert - All UUIDs are unique in Postgres
        Assert.Equal(10, QueryPg<long>("SELECT COUNT(DISTINCT id) FROM uuid_test"));

        // Assert - All UUIDs are unique in SQLite
        Assert.Equal(10, QuerySqlite<long>("SELECT COUNT(DISTINCT id) FROM uuid_test"));
    }

    [Fact]
    public void LqlTimestamp_AllTimeTypes_WorkBothPlatforms()
    {
        // Arrange - Test all time-related LQL functions
        VerifySharedContinuation2(
            input0: Fixture_LqlTimestamp_AllTimeTypes_WorkBothPlatforms_1,
            value0: "INSERT INTO time_test (id) VALUES (1)",
            value1: "INSERT INTO time_test (id) VALUES (1)"
        );

        // Assert - All time values are populated
        Assert.True(
            QueryPg<DateTime>("SELECT ts1 FROM time_test WHERE id = 1") > DateTime.MinValue
        );
        Assert.True(
            QueryPg<DateTime>("SELECT ts2 FROM time_test WHERE id = 1") > DateTime.MinValue
        );
        Assert.True(QueryPg<DateTime>("SELECT d FROM time_test WHERE id = 1") > DateTime.MinValue);
        var pgTime = QueryPg<TimeSpan>("SELECT t FROM time_test WHERE id = 1");
        Assert.True(pgTime >= TimeSpan.Zero);

        // SQLite returns strings for all temporal types
        Assert.False(
            string.IsNullOrEmpty(QuerySqlite<string>("SELECT ts1 FROM time_test WHERE id = 1"))
        );
        Assert.False(
            string.IsNullOrEmpty(QuerySqlite<string>("SELECT ts2 FROM time_test WHERE id = 1"))
        );
        Assert.False(
            string.IsNullOrEmpty(QuerySqlite<string>("SELECT d FROM time_test WHERE id = 1"))
        );
        Assert.False(
            string.IsNullOrEmpty(QuerySqlite<string>("SELECT t FROM time_test WHERE id = 1"))
        );
    }

    // =========================================================================
    // HELPER METHODS
    // =========================================================================

    private void ApplySchema(
        NpgsqlConnection conn,
        SchemaDefinition schema,
        Func<SchemaOperation, string> generator
    )
    {
        var operations = SqliteTestDb.CalculateChanges(
            inspect: () =>
                (Ok(actual: PostgresSchemaInspector.Inspect(conn, "public", _logger))).Value,
            desired: schema,
            allowDestructive: false,
            logger: _logger
        );
        _ = MigrationRunner.Apply(conn, operations, generator, MigrationOptions.Default, _logger);
    }

    private void ApplySchema(
        SqliteConnection conn,
        SchemaDefinition schema,
        Func<SchemaOperation, string> generator
    )
    {
        var operations = SqliteTestDb.CalculateChanges(conn, schema, _logger);
        _ = MigrationRunner.Apply(conn, operations, generator, MigrationOptions.Default, _logger);
    }

    private void ExecutePg(string sql)
    {
        DatabaseCommands.Execute(
            connection: _pgConnection,
            configure: command => command.CommandText = sql
        );
    }

    private void ExecuteSqlite(string sql)
    {
        DatabaseCommands.Execute(
            connection: _sqliteConnection,
            configure: command => command.CommandText = sql
        );
    }

    private T QueryPg<T>(string sql) => Query<T>(connection: _pgConnection, sql: sql);

    private T QuerySqlite<T>(string sql) => Query<T>(connection: _sqliteConnection, sql: sql);

    private static T Query<T>(global::System.Data.Common.DbConnection connection, string sql)
    {
        return (T)
            global::Tests.Shared.DatabaseCommands.Scalar(
                connection: connection,
                configure: command => command.CommandText = sql
            )!;
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private (string PgDdl, string SqliteDdl) ReadBothDdl(
        Func<SchemaDefinition> input0,
        string tableName
    ) =>
        VerifySharedPhase4(
            input0: input0,
            input1: _pgConnection,
            input2: _sqliteConnection,
            value0: tableName,
            value1: tableName
        );

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private void AssertBothColumnCount(string tableName, int expected)
    {
        Assert.Equal(expected, GetPostgresColumns(_pgConnection, tableName).Count);
        Assert.Equal(expected, GetSqliteColumns(_sqliteConnection, tableName).Count);
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private void AssertPgSqliteLong(long expected, string pgSql, string sqliteSql)
    {
        Assert.Equal(
            expected,
            Convert.ToInt64(
                QueryPg<object>(pgSql),
                System.Globalization.CultureInfo.InvariantCulture
            )
        );
        Assert.Equal(
            expected,
            Convert.ToInt64(
                QuerySqlite<object>(sqliteSql),
                System.Globalization.CultureInfo.InvariantCulture
            )
        );
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private void AssertPgSqlite<TPg, TSqlite>(
        TPg pgExpected,
        string pgSql,
        TSqlite sqliteExpected,
        string sqliteSql
    )
    {
        Assert.Equal(pgExpected, QueryPg<TPg>(pgSql));
        Assert.Equal(sqliteExpected, QuerySqlite<TSqlite>(sqliteSql));
    }

    // =========================================================================
    // DDL VERIFICATION HELPERS - Simple string-based verification
    // =========================================================================

    /// <summary>
    /// Get PostgreSQL table DDL for verification via pg_get_tabledef or information_schema.
    /// Returns a string representation of the table structure for comparison.
    /// </summary>
    private static string GetPostgresTableDdl(
        NpgsqlConnection conn,
        string tableName,
        string schema = "public"
    )
    {
        // Query column info from information_schema and build a normalized DDL representation
        using var cmd = conn.CreateCommand();
        VerifyUnifiedWorkflow102(
            input0: cmd,
            input1: Data_GetPostgresTableDdl_1,
            input2: schema,
            input3: tableName
        );

        var ddlParts = new List<string> { $"CREATE TABLE {schema}.{tableName} (" };

        using var reader = cmd.ExecuteReader();
        var columns = new List<string>();
        while (reader.Read())
        {
            var colName = reader.GetString(0);
            var dataType = reader.GetString(1);
            var isNullable = reader.GetString(2);
            var colDefault = reader.IsDBNull(3) ? null : reader.GetString(3);

            var colDef = $"  {colName} {dataType}";
            if (isNullable == "NO")
                colDef += " NOT NULL";
            if (colDefault is not null)
                colDef += $" DEFAULT {colDefault}";
            columns.Add(colDef);
        }
        reader.Close();

        // Get primary key info
        using var pkCmd = conn.CreateCommand();
        VerifyUnifiedWorkflow102(
            input0: pkCmd,
            input1: Data_GetPostgresTableDdl_2,
            input2: schema,
            input3: tableName
        );

        var pkColumns = DatabaseCommands.ReadStrings(pkCmd);

        ddlParts.Add(string.Join(",\n", columns));
        if (pkColumns.Count > 0)
        {
            ddlParts.Add($"  PRIMARY KEY ({string.Join(", ", pkColumns)})");
        }
        ddlParts.Add(")");

        return string.Join("\n", ddlParts);
    }

    /// <summary>
    /// Get SQLite table DDL using sqlite_master.
    /// </summary>
    private static string GetSqliteTableDdl(SqliteConnection conn, string tableName)
    {
        var result = global::Tests.Shared.DatabaseCommands.Scalar(
            connection: conn,
            configure: command =>
                command.CommandText =
                    "SELECT sql FROM sqlite_master WHERE type='table' AND name=@name",
            parameters: () => [("@name", tableName)]
        );

        return result?.ToString() ?? "";
    }

    /// <summary>
    /// Get list of column names for a PostgreSQL table.
    /// </summary>
    private static List<string> GetPostgresColumns(
        NpgsqlConnection conn,
        string tableName,
        string schema = "public"
    )
    {
        using var cmd = conn.CreateCommand();
        VerifyUnifiedWorkflow102(
            input0: cmd,
            input1: Data_GetPostgresColumns_1,
            input2: schema,
            input3: tableName
        );

        return DatabaseCommands.ReadStrings(cmd);
    }

    /// <summary>
    /// Get list of column names for a SQLite table.
    /// </summary>
    private static List<string> GetSqliteColumns(SqliteConnection conn, string tableName)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"PRAGMA table_info({tableName})";

        return DatabaseCommands.ReadStrings(cmd, ordinal: 1);
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private void ApplyBothSchemasAndReadDdl(
        global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition input0,
        string value0,
        string value1
    )
    {
        // Act
        ApplySchema(_pgConnection, input0, PostgresDdlGenerator.Generate);
        ApplySchema(_sqliteConnection, input0, SqliteDdlGenerator.Generate);

        ExecutePg(value0);
        ExecuteSqlite(value1);
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private void AssertDatabaseQueryValues(
        string value0,
        string value1,
        string value2,
        string value3,
        string value4,
        string value5
    )
    {
        Assert.Equal(value0, QuerySqlite<string>(value1));

        Assert.Equal(value2, QueryPg<string>(value3));
        Assert.Equal(value4, QuerySqlite<string>(value5));
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private void VerifySharedChecks3(Func<SchemaDefinition> input0, string value0, string value1) =>
        VerifySharedContinuation2(input0: input0, value0: value0, value1: value1);

    // Implements [CI-TEST-SHARED-SCENARIOS].
    internal static void VerifyUnifiedWorkflow102(
        NpgsqlCommand input0,
        string input1,
        string input2,
        string input3
    )
    {
        input0.CommandText = input1;
        input0.Parameters.AddWithValue("@schema", input2);
        input0.Parameters.AddWithValue("@table", input3);
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private void VerifySharedContinuation2(
        Func<SchemaDefinition> input0,
        string value0,
        string value1
    )
    { // Arrange - Strings must be quoted with single quotes in LQL
        var schema = input0();

        // Act
        ApplyBothSchemasAndReadDdl(input0: schema, value0: value0, value1: value1);
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private (string, string) VerifySharedContinuation6(
        NpgsqlConnection input0,
        SchemaDefinition input1,
        SqliteConnection input2,
        string value0,
        string value1
    )
    {
        // Act - Apply to both databases
        ApplySchema(input0, input1, PostgresDdlGenerator.Generate);
        ApplySchema(input2, input1, SqliteDdlGenerator.Generate);

        // VERIFY DDL: Table structure is correct
        var pgDdl = GetPostgresTableDdl(input0, value0);
        var sqliteDdl = GetSqliteTableDdl(input2, value1);

        return (pgDdl, sqliteDdl);
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    internal (string, string) VerifySharedPhase4(
        Func<SchemaDefinition> input0,
        NpgsqlConnection input1,
        SqliteConnection input2,
        string value0,
        string value1
    )
    { // Arrange - Same LQL schema for both platforms
        var schema = input0();

        // Act - Apply to both databases
        var (pgDdl, sqliteDdl) = VerifySharedContinuation6(
            input0: input1,
            input1: schema,
            input2: input2,
            value0: value0,
            value1: value1
        );

        return (pgDdl, sqliteDdl);
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    internal static void VerifySharedPhase7(List<string> input0, int value0, string value2)
    {
        Assert.Equal(value0, input0.Count);
        Assert.Contains("id", input0);
        Assert.Contains(value2, input0);
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private void VerifyReusableWorkflow4(
        decimal value0,
        string value1,
        double value2,
        string value3,
        int value4
    )
    {
        // Assert - Same decimal values (within floating-point tolerance for SQLite)
        Assert.Equal(value0, QueryPg<decimal>(value1));
        Assert.Equal(value2, QuerySqlite<double>(value3), value4);
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private (DateTime, string) VerifyReusableWorkflow23(
        Action<Func<SchemaDefinition>, string, string> input0,
        Func<SchemaDefinition> input1,
        string value0,
        string value1,
        string value2,
        string value3
    )
    { // Arrange
        input0(arg1: input1, arg2: value0, arg3: value1);

        // Assert - Should have valid timestamps
        var pgValue = QueryPg<DateTime>(value2);
        var sqliteValue = QuerySqlite<string>(value3);

        return (pgValue, sqliteValue);
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private void VerifyCompletionWorkflow1(
        Action<string, string, string, string, string, string> input0,
        string value0,
        string value1,
        string value2,
        string value3,
        string value4,
        string value5,
        string value6,
        string value7
    )
    {
        // Assert - Same string values on both platforms
        Assert.Equal(value0, QueryPg<string>(value1));
        input0(arg1: value2, arg2: value3, arg3: value4, arg4: value5, arg5: value6, arg6: value7);
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private void VerifyCompletionWorkflow2(
        string value1,
        string value3,
        string value5,
        string value7
    )
    {
        // Assert - All flags match expected values
        AssertPgSqlite<bool, long>(true, value1, 1L, value3);
        AssertPgSqlite<bool, long>(false, value5, 0L, value7);
    }
}
