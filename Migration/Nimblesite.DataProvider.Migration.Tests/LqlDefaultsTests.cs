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
public sealed class LqlDefaultsTests(PostgresContainerFixture fixture) : IAsyncLifetime
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

    [Fact]
    public void LqlBoolean_True_WorksOnBothPlatforms()
    {
        // Arrange - Same LQL schema for both platforms
        var schema = Schema
            .Define("Test")
            .Table(
                "public",
                "settings",
                t =>
                    t.Column("id", PortableTypes.Int, c => c.PrimaryKey())
                        .Column("enabled", PortableTypes.Boolean, c => c.DefaultLql("true"))
            )
            .Build();

        // Act - Apply to both databases
        ApplySchema(_pgConnection, schema, PostgresDdlGenerator.Generate);
        ApplySchema(_sqliteConnection, schema, SqliteDdlGenerator.Generate);

        // VERIFY DDL: Table structure is correct
        var pgDdl = GetPostgresTableDdl(_pgConnection, "settings");
        var sqliteDdl = GetSqliteTableDdl(_sqliteConnection, "settings");

        // PostgreSQL: boolean column with DEFAULT true
        Assert.Contains("enabled", pgDdl);
        Assert.Contains("DEFAULT true", pgDdl);

        // SQLite: integer column with DEFAULT 1 (true = 1)
        Assert.Contains("[enabled]", sqliteDdl);
        Assert.Contains("DEFAULT 1", sqliteDdl);

        // VERIFY COLUMNS: Exactly 2 columns, no extras
        var pgColumns = GetPostgresColumns(_pgConnection, "settings");
        Assert.Equal(2, pgColumns.Count);
        Assert.Contains("id", pgColumns);
        Assert.Contains("enabled", pgColumns);

        var sqliteColumns = GetSqliteColumns(_sqliteConnection, "settings");
        Assert.Equal(2, sqliteColumns.Count);
        Assert.Contains("id", sqliteColumns);
        Assert.Contains("enabled", sqliteColumns);

        // Insert without specifying 'enabled'
        ExecutePg("INSERT INTO settings (id) VALUES (1)");
        ExecuteSqlite("INSERT INTO settings (id) VALUES (1)");

        // VERIFY RUNTIME: Defaults applied correctly
        var pgValue = QueryPg<bool>("SELECT enabled FROM settings WHERE id = 1");
        var sqliteValue = QuerySqlite<long>("SELECT enabled FROM settings WHERE id = 1");

        Assert.True(pgValue); // Postgres: true
        Assert.Equal(1, sqliteValue); // SQLite: 1 (represents true)
    }

    [Fact]
    public void LqlBoolean_False_WorksOnBothPlatforms()
    {
        // Arrange
        var schema = Schema
            .Define("Test")
            .Table(
                "public",
                "flags",
                t =>
                    t.Column("id", PortableTypes.Int, c => c.PrimaryKey())
                        .Column("disabled", PortableTypes.Boolean, c => c.DefaultLql("false"))
            )
            .Build();

        // Act
        ApplySchema(_pgConnection, schema, PostgresDdlGenerator.Generate);
        ApplySchema(_sqliteConnection, schema, SqliteDdlGenerator.Generate);

        // VERIFY DDL: Table structure is correct
        var pgDdl = GetPostgresTableDdl(_pgConnection, "flags");
        var sqliteDdl = GetSqliteTableDdl(_sqliteConnection, "flags");

        // PostgreSQL: boolean column with DEFAULT false
        Assert.Contains("disabled", pgDdl);
        Assert.Contains("DEFAULT false", pgDdl);

        // SQLite: integer column with DEFAULT 0 (false = 0)
        Assert.Contains("[disabled]", sqliteDdl);
        Assert.Contains("DEFAULT 0", sqliteDdl);

        // VERIFY COLUMNS: Exactly 2 columns, no extras
        Assert.Equal(2, GetPostgresColumns(_pgConnection, "flags").Count);
        Assert.Equal(2, GetSqliteColumns(_sqliteConnection, "flags").Count);

        ExecutePg("INSERT INTO flags (id) VALUES (1)");
        ExecuteSqlite("INSERT INTO flags (id) VALUES (1)");

        // VERIFY RUNTIME: Defaults applied correctly
        var pgValue = QueryPg<bool>("SELECT disabled FROM flags WHERE id = 1");
        var sqliteValue = QuerySqlite<long>("SELECT disabled FROM flags WHERE id = 1");

        Assert.False(pgValue); // Postgres: false
        Assert.Equal(0, sqliteValue); // SQLite: 0 (represents false)
    }

    // =========================================================================
    // TIMESTAMP DEFAULTS - now() and current_timestamp()
    // =========================================================================

    [Fact]
    public void LqlNow_DefaultsToCurrentTime_BothPlatforms()
    {
        // Arrange
        var schema = Schema
            .Define("Test")
            .Table(
                "public",
                "events",
                t =>
                    t.Column("id", PortableTypes.Int, c => c.PrimaryKey())
                        .Column(
                            "created_at",
                            PortableTypes.DateTimeOffset,
                            c => c.DefaultLql("now()")
                        )
            )
            .Build();

        var beforeTest = DateTime.UtcNow.AddSeconds(-1);

        // Act
        ApplySchema(_pgConnection, schema, PostgresDdlGenerator.Generate);
        ApplySchema(_sqliteConnection, schema, SqliteDdlGenerator.Generate);

        // VERIFY DDL: Default expressions are correctly translated
        var pgDdl = GetPostgresTableDdl(_pgConnection, "events");
        var sqliteDdl = GetSqliteTableDdl(_sqliteConnection, "events");

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
        var schema = Schema
            .Define("Test")
            .Table(
                "public",
                "logs",
                t =>
                    t.Column("id", PortableTypes.Int, c => c.PrimaryKey())
                        .Column(
                            "timestamp",
                            PortableTypes.DateTimeOffset,
                            c => c.DefaultLql("current_timestamp()")
                        )
            )
            .Build();

        // Act
        ApplySchema(_pgConnection, schema, PostgresDdlGenerator.Generate);
        ApplySchema(_sqliteConnection, schema, SqliteDdlGenerator.Generate);

        ExecutePg("INSERT INTO logs (id) VALUES (1)");
        ExecuteSqlite("INSERT INTO logs (id) VALUES (1)");

        // Assert - Should have valid timestamps
        var pgValue = QueryPg<DateTime>("SELECT timestamp FROM logs WHERE id = 1");
        var sqliteValue = QuerySqlite<string>("SELECT timestamp FROM logs WHERE id = 1");

        Assert.True(pgValue > DateTime.MinValue);
        Assert.False(string.IsNullOrEmpty(sqliteValue));
    }

    [Fact]
    public void LqlCurrentDate_ReturnsDateOnly_BothPlatforms()
    {
        // Arrange
        var schema = Schema
            .Define("Test")
            .Table(
                "public",
                "daily_records",
                t =>
                    t.Column("id", PortableTypes.Int, c => c.PrimaryKey())
                        .Column(
                            "record_date",
                            PortableTypes.Date,
                            c => c.DefaultLql("current_date()")
                        )
            )
            .Build();

        // Act
        ApplySchema(_pgConnection, schema, PostgresDdlGenerator.Generate);
        ApplySchema(_sqliteConnection, schema, SqliteDdlGenerator.Generate);

        ExecutePg("INSERT INTO daily_records (id) VALUES (1)");
        ExecuteSqlite("INSERT INTO daily_records (id) VALUES (1)");

        // Assert
        var pgValue = QueryPg<DateTime>("SELECT record_date FROM daily_records WHERE id = 1");
        var sqliteValue = QuerySqlite<string>("SELECT record_date FROM daily_records WHERE id = 1");

        // Postgres: Date value
        Assert.Equal(DateTime.UtcNow.Date, pgValue.Date);

        // SQLite: Date string (e.g., "2025-01-15")
        Assert.True(DateTime.TryParse(sqliteValue, out var sqliteDt));
    }

    // =========================================================================
    // NUMERIC DEFAULTS - integers and decimals
    // =========================================================================

    [Fact]
    public void LqlNumericInteger_DefaultsCorrectly_BothPlatforms()
    {
        // Arrange
        var schema = Schema
            .Define("Test")
            .Table(
                "public",
                "counters",
                t =>
                    t.Column("id", PortableTypes.Int, c => c.PrimaryKey())
                        .Column("count", PortableTypes.Int, c => c.DefaultLql("42"))
                        .Column("negative", PortableTypes.Int, c => c.DefaultLql("-100"))
                        .Column("zero", PortableTypes.Int, c => c.DefaultLql("0"))
            )
            .Build();

        // Act
        ApplySchema(_pgConnection, schema, PostgresDdlGenerator.Generate);
        ApplySchema(_sqliteConnection, schema, SqliteDdlGenerator.Generate);

        ExecutePg("INSERT INTO counters (id) VALUES (1)");
        ExecuteSqlite("INSERT INTO counters (id) VALUES (1)");

        // Assert - Same integer values on both platforms
        Assert.Equal(42, QueryPg<int>("SELECT count FROM counters WHERE id = 1"));
        Assert.Equal(42, QuerySqlite<long>("SELECT count FROM counters WHERE id = 1"));

        Assert.Equal(-100, QueryPg<int>("SELECT negative FROM counters WHERE id = 1"));
        Assert.Equal(-100, QuerySqlite<long>("SELECT negative FROM counters WHERE id = 1"));

        Assert.Equal(0, QueryPg<int>("SELECT zero FROM counters WHERE id = 1"));
        Assert.Equal(0, QuerySqlite<long>("SELECT zero FROM counters WHERE id = 1"));
    }

    [Fact]
    public void LqlNumericDecimal_DefaultsCorrectly_BothPlatforms()
    {
        // Arrange
        var schema = Schema
            .Define("Test")
            .Table(
                "public",
                "prices",
                t =>
                    t.Column("id", PortableTypes.Int, c => c.PrimaryKey())
                        .Column("amount", PortableTypes.Decimal(10, 2), c => c.DefaultLql("99.99"))
                        .Column(
                            "tax_rate",
                            PortableTypes.Decimal(5, 4),
                            c => c.DefaultLql("0.0825")
                        )
                        .Column(
                            "discount",
                            PortableTypes.Decimal(5, 2),
                            c => c.DefaultLql("-10.50")
                        )
            )
            .Build();

        // Act
        ApplySchema(_pgConnection, schema, PostgresDdlGenerator.Generate);
        ApplySchema(_sqliteConnection, schema, SqliteDdlGenerator.Generate);

        ExecutePg("INSERT INTO prices (id) VALUES (1)");
        ExecuteSqlite("INSERT INTO prices (id) VALUES (1)");

        // Assert - Same decimal values (within floating-point tolerance for SQLite)
        Assert.Equal(99.99m, QueryPg<decimal>("SELECT amount FROM prices WHERE id = 1"));
        Assert.Equal(99.99, QuerySqlite<double>("SELECT amount FROM prices WHERE id = 1"), 2);

        Assert.Equal(0.0825m, QueryPg<decimal>("SELECT tax_rate FROM prices WHERE id = 1"));
        Assert.Equal(0.0825, QuerySqlite<double>("SELECT tax_rate FROM prices WHERE id = 1"), 4);

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
        var schema = Schema
            .Define("Test")
            .Table(
                "public",
                "statuses",
                t =>
                    t.Column("id", PortableTypes.Int, c => c.PrimaryKey())
                        .Column("status", PortableTypes.VarChar(50), c => c.DefaultLql("'active'"))
                        .Column(
                            "category",
                            PortableTypes.VarChar(100),
                            c => c.DefaultLql("'uncategorized'")
                        )
                        .Column("empty", PortableTypes.VarChar(10), c => c.DefaultLql("''"))
            )
            .Build();

        // Act
        ApplySchema(_pgConnection, schema, PostgresDdlGenerator.Generate);
        ApplySchema(_sqliteConnection, schema, SqliteDdlGenerator.Generate);

        ExecutePg("INSERT INTO statuses (id) VALUES (1)");
        ExecuteSqlite("INSERT INTO statuses (id) VALUES (1)");

        // Assert - Same string values on both platforms
        Assert.Equal("active", QueryPg<string>("SELECT status FROM statuses WHERE id = 1"));
        Assert.Equal("active", QuerySqlite<string>("SELECT status FROM statuses WHERE id = 1"));

        Assert.Equal(
            "uncategorized",
            QueryPg<string>("SELECT category FROM statuses WHERE id = 1")
        );
        Assert.Equal(
            "uncategorized",
            QuerySqlite<string>("SELECT category FROM statuses WHERE id = 1")
        );

        Assert.Equal("", QueryPg<string>("SELECT empty FROM statuses WHERE id = 1"));
        Assert.Equal("", QuerySqlite<string>("SELECT empty FROM statuses WHERE id = 1"));
    }

    // =========================================================================
    // UUID DEFAULTS - gen_uuid()
    // =========================================================================

    [Fact]
    public void LqlGenUuid_GeneratesValidUuid_BothPlatforms()
    {
        // Arrange
        var schema = Schema
            .Define("Test")
            .Table(
                "public",
                "entities",
                t =>
                    t.Column("id", PortableTypes.Uuid, c => c.PrimaryKey().DefaultLql("gen_uuid()"))
                        .Column("name", PortableTypes.VarChar(100))
            )
            .Build();

        // Act
        ApplySchema(_pgConnection, schema, PostgresDdlGenerator.Generate);
        ApplySchema(_sqliteConnection, schema, SqliteDdlGenerator.Generate);

        // VERIFY DDL: UUID generation is correctly translated
        var pgDdl = GetPostgresTableDdl(_pgConnection, "entities");
        var sqliteDdl = GetSqliteTableDdl(_sqliteConnection, "entities");

        // PostgreSQL: gen_uuid() translates to gen_random_uuid()
        Assert.Contains("id", pgDdl);
        Assert.Contains("gen_random_uuid()", pgDdl.ToLowerInvariant());

        // SQLite: gen_uuid() translates to complex hex expression
        Assert.Contains("[id]", sqliteDdl);
        Assert.Contains("hex(randomblob", sqliteDdl.ToLowerInvariant());

        // VERIFY COLUMNS: Exactly 2 columns
        var pgCols = GetPostgresColumns(_pgConnection, "entities");
        Assert.Equal(2, pgCols.Count);
        Assert.Contains("id", pgCols);
        Assert.Contains("name", pgCols);

        var sqliteCols = GetSqliteColumns(_sqliteConnection, "entities");
        Assert.Equal(2, sqliteCols.Count);
        Assert.Contains("id", sqliteCols);
        Assert.Contains("name", sqliteCols);

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
        var schema = Schema
            .Define("Test")
            .Table(
                "public",
                "items",
                t =>
                    t.Column("id", PortableTypes.Uuid, c => c.PrimaryKey().DefaultLql("uuid()"))
                        .Column("label", PortableTypes.VarChar(50))
            )
            .Build();

        // Act
        ApplySchema(_pgConnection, schema, PostgresDdlGenerator.Generate);
        ApplySchema(_sqliteConnection, schema, SqliteDdlGenerator.Generate);

        ExecutePg("INSERT INTO items (label) VALUES ('item1')");
        ExecuteSqlite("INSERT INTO items (label) VALUES ('item1')");

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
        var schema = Schema
            .Define("Test")
            .Table(
                "public",
                "audit_records",
                t =>
                    t.Column("id", PortableTypes.Uuid, c => c.PrimaryKey().DefaultLql("gen_uuid()"))
                        .Column("action", PortableTypes.VarChar(100), c => c.NotNull())
                        .Column("is_success", PortableTypes.Boolean, c => c.DefaultLql("true"))
                        .Column("retry_count", PortableTypes.Int, c => c.DefaultLql("0"))
                        .Column("priority", PortableTypes.Decimal(3, 1), c => c.DefaultLql("5.0"))
                        .Column("status", PortableTypes.VarChar(20), c => c.DefaultLql("'pending'"))
                        .Column(
                            "created_at",
                            PortableTypes.DateTimeOffset,
                            c => c.DefaultLql("now()")
                        )
            )
            .Build();

        // Act
        ApplySchema(_pgConnection, schema, PostgresDdlGenerator.Generate);
        ApplySchema(_sqliteConnection, schema, SqliteDdlGenerator.Generate);

        // VERIFY DDL: All defaults are correctly translated
        var pgDdl = GetPostgresTableDdl(_pgConnection, "audit_records");
        var sqliteDdl = GetSqliteTableDdl(_sqliteConnection, "audit_records");

        // PostgreSQL DDL verification
        Assert.Contains("gen_random_uuid()", pgDdl.ToLowerInvariant()); // UUID
        Assert.Contains("DEFAULT true", pgDdl); // boolean true
        Assert.Contains("DEFAULT 0", pgDdl); // integer 0
        Assert.Contains("DEFAULT 5.0", pgDdl); // decimal
        Assert.Contains("DEFAULT 'pending'", pgDdl); // string
        Assert.Contains("CURRENT_TIMESTAMP", pgDdl.ToUpperInvariant()); // now()

        // SQLite DDL verification
        Assert.Contains("hex(randomblob", sqliteDdl.ToLowerInvariant()); // UUID
        Assert.Contains("DEFAULT 1", sqliteDdl); // boolean true = 1
        Assert.Contains("DEFAULT 0", sqliteDdl); // integer 0
        Assert.Contains("DEFAULT 5.0", sqliteDdl); // decimal
        Assert.Contains("DEFAULT 'pending'", sqliteDdl); // string
        Assert.Contains("datetime('now')", sqliteDdl.ToLowerInvariant()); // now()

        // VERIFY COLUMNS: Exactly 7 columns with correct names
        var pgCols = GetPostgresColumns(_pgConnection, "audit_records");
        Assert.Equal(7, pgCols.Count);
        Assert.Contains("id", pgCols);
        Assert.Contains("action", pgCols);
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
        var schema = Schema
            .Define("Test")
            .Table(
                "public",
                "rerunnable",
                t =>
                    t.Column("id", PortableTypes.Int, c => c.PrimaryKey())
                        .Column("active", PortableTypes.Boolean, c => c.DefaultLql("true"))
                        .Column("count", PortableTypes.Int, c => c.DefaultLql("1"))
            )
            .Build();

        // Act - Apply FIRST time
        ApplySchema(_pgConnection, schema, PostgresDdlGenerator.Generate);
        ApplySchema(_sqliteConnection, schema, SqliteDdlGenerator.Generate);

        // Capture DDL BEFORE second application
        var pgDdlBefore = GetPostgresTableDdl(_pgConnection, "rerunnable");
        var sqliteDdlBefore = GetSqliteTableDdl(_sqliteConnection, "rerunnable");

        // Apply SECOND time
        ApplySchema(_pgConnection, schema, PostgresDdlGenerator.Generate);
        ApplySchema(_sqliteConnection, schema, SqliteDdlGenerator.Generate);

        // Capture DDL AFTER second application
        var pgDdlAfter = GetPostgresTableDdl(_pgConnection, "rerunnable");
        var sqliteDdlAfter = GetSqliteTableDdl(_sqliteConnection, "rerunnable");

        // ASSERT 1: DDL is IDENTICAL before and after (proves true idempotency)
        Assert.Equal(pgDdlBefore, pgDdlAfter);
        Assert.Equal(sqliteDdlBefore, sqliteDdlAfter);

        // ASSERT 2: Table structure is EXACTLY what we defined
        // PostgreSQL: Verify table exists with correct columns
        Assert.True(SchemaVerifier.PostgresTableExists(_pgConnection, "rerunnable", "public"));
        var pgColumns = GetPostgresColumns(_pgConnection, "rerunnable", "public");
        Assert.Equal(3, pgColumns.Count); // id, active, count - NO EXTRA COLUMNS
        Assert.Contains("id", pgColumns);
        Assert.Contains("active", pgColumns);
        Assert.Contains("count", pgColumns);

        // SQLite: Verify table exists with correct columns
        Assert.True(SchemaVerifier.SqliteTableExists(_sqliteConnection, "rerunnable"));
        var sqliteColumns = GetSqliteColumns(_sqliteConnection, "rerunnable");
        Assert.Equal(3, sqliteColumns.Count); // id, active, count - NO EXTRA COLUMNS
        Assert.Contains("id", sqliteColumns);
        Assert.Contains("active", sqliteColumns);
        Assert.Contains("count", sqliteColumns);

        // ASSERT 3: Defaults are CORRECT in DDL
        // PostgreSQL: Check defaults in DDL
        Assert.Contains("DEFAULT true", pgDdlAfter); // boolean true
        Assert.Contains("DEFAULT 1", pgDdlAfter); // integer 1

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
    public void LqlNumeric_LargeInteger_SameValueBothPlatforms()
    {
        // Arrange - Test large integer values (near int32 boundaries)
        var schema = Schema
            .Define("Test")
            .Table(
                "public",
                "large_nums",
                t =>
                    t.Column("id", PortableTypes.Int, c => c.PrimaryKey())
                        .Column("big_pos", PortableTypes.BigInt, c => c.DefaultLql("2147483647")) // int32 max
                        .Column("big_neg", PortableTypes.BigInt, c => c.DefaultLql("-2147483648")) // int32 min
            )
            .Build();

        // Act
        ApplySchema(_pgConnection, schema, PostgresDdlGenerator.Generate);
        ApplySchema(_sqliteConnection, schema, SqliteDdlGenerator.Generate);

        ExecutePg("INSERT INTO large_nums (id) VALUES (1)");
        ExecuteSqlite("INSERT INTO large_nums (id) VALUES (1)");

        // Assert - Same values on both platforms
        Assert.Equal(2147483647, QueryPg<long>("SELECT big_pos FROM large_nums WHERE id = 1"));
        Assert.Equal(2147483647, QuerySqlite<long>("SELECT big_pos FROM large_nums WHERE id = 1"));

        Assert.Equal(-2147483648, QueryPg<long>("SELECT big_neg FROM large_nums WHERE id = 1"));
        Assert.Equal(-2147483648, QuerySqlite<long>("SELECT big_neg FROM large_nums WHERE id = 1"));
    }

    [Fact]
    public void LqlNumeric_VerySmallDecimal_SameValueBothPlatforms()
    {
        // Arrange - Test precision with very small decimal values
        var schema = Schema
            .Define("Test")
            .Table(
                "public",
                "precision_test",
                t =>
                    t.Column("id", PortableTypes.Int, c => c.PrimaryKey())
                        .Column(
                            "tiny",
                            PortableTypes.Decimal(10, 8),
                            c => c.DefaultLql("0.00000001")
                        )
                        .Column(
                            "scientific",
                            PortableTypes.Decimal(15, 10),
                            c => c.DefaultLql("3.1415926535")
                        )
            )
            .Build();

        // Act
        ApplySchema(_pgConnection, schema, PostgresDdlGenerator.Generate);
        ApplySchema(_sqliteConnection, schema, SqliteDdlGenerator.Generate);

        ExecutePg("INSERT INTO precision_test (id) VALUES (1)");
        ExecuteSqlite("INSERT INTO precision_test (id) VALUES (1)");

        // Assert
        Assert.Equal(0.00000001m, QueryPg<decimal>("SELECT tiny FROM precision_test WHERE id = 1"));
        Assert.Equal(
            0.00000001,
            QuerySqlite<double>("SELECT tiny FROM precision_test WHERE id = 1"),
            8
        );

        Assert.Equal(
            3.1415926535m,
            QueryPg<decimal>("SELECT scientific FROM precision_test WHERE id = 1")
        );
        Assert.Equal(
            3.1415926535,
            QuerySqlite<double>("SELECT scientific FROM precision_test WHERE id = 1"),
            10
        );
    }

    [Fact]
    public void LqlString_SpecialCharacters_SameValueBothPlatforms()
    {
        // Arrange - Test strings with special characters (escaped in LQL as SQL single quotes)
        var schema = Schema
            .Define("Test")
            .Table(
                "public",
                "special_strings",
                t =>
                    t.Column("id", PortableTypes.Int, c => c.PrimaryKey())
                        .Column(
                            "with_spaces",
                            PortableTypes.VarChar(50),
                            c => c.DefaultLql("'hello world'")
                        )
                        .Column(
                            "with_numbers",
                            PortableTypes.VarChar(50),
                            c => c.DefaultLql("'test123'")
                        )
                        .Column(
                            "with_hyphen",
                            PortableTypes.VarChar(50),
                            c => c.DefaultLql("'foo-bar-baz'")
                        )
                        .Column(
                            "with_underscore",
                            PortableTypes.VarChar(50),
                            c => c.DefaultLql("'snake_case_value'")
                        )
            )
            .Build();

        // Act
        ApplySchema(_pgConnection, schema, PostgresDdlGenerator.Generate);
        ApplySchema(_sqliteConnection, schema, SqliteDdlGenerator.Generate);

        ExecutePg("INSERT INTO special_strings (id) VALUES (1)");
        ExecuteSqlite("INSERT INTO special_strings (id) VALUES (1)");

        // Assert - Same string values on both platforms
        Assert.Equal(
            "hello world",
            QueryPg<string>("SELECT with_spaces FROM special_strings WHERE id = 1")
        );
        Assert.Equal(
            "hello world",
            QuerySqlite<string>("SELECT with_spaces FROM special_strings WHERE id = 1")
        );

        Assert.Equal(
            "test123",
            QueryPg<string>("SELECT with_numbers FROM special_strings WHERE id = 1")
        );
        Assert.Equal(
            "test123",
            QuerySqlite<string>("SELECT with_numbers FROM special_strings WHERE id = 1")
        );

        Assert.Equal(
            "foo-bar-baz",
            QueryPg<string>("SELECT with_hyphen FROM special_strings WHERE id = 1")
        );
        Assert.Equal(
            "foo-bar-baz",
            QuerySqlite<string>("SELECT with_hyphen FROM special_strings WHERE id = 1")
        );

        Assert.Equal(
            "snake_case_value",
            QueryPg<string>("SELECT with_underscore FROM special_strings WHERE id = 1")
        );
        Assert.Equal(
            "snake_case_value",
            QuerySqlite<string>("SELECT with_underscore FROM special_strings WHERE id = 1")
        );
    }

    [Fact]
    public void LqlBoolean_MultipleColumns_AllDefaultCorrectly()
    {
        // Arrange - Test multiple boolean defaults in various combinations
        var schema = Schema
            .Define("Test")
            .Table(
                "public",
                "feature_flags",
                t =>
                    t.Column("id", PortableTypes.Int, c => c.PrimaryKey())
                        .Column("flag_a", PortableTypes.Boolean, c => c.DefaultLql("true"))
                        .Column("flag_b", PortableTypes.Boolean, c => c.DefaultLql("false"))
                        .Column("flag_c", PortableTypes.Boolean, c => c.DefaultLql("true"))
                        .Column("flag_d", PortableTypes.Boolean, c => c.DefaultLql("false"))
            )
            .Build();

        // Act
        ApplySchema(_pgConnection, schema, PostgresDdlGenerator.Generate);
        ApplySchema(_sqliteConnection, schema, SqliteDdlGenerator.Generate);

        ExecutePg("INSERT INTO feature_flags (id) VALUES (1)");
        ExecuteSqlite("INSERT INTO feature_flags (id) VALUES (1)");

        // Assert - All flags match expected values
        Assert.True(QueryPg<bool>("SELECT flag_a FROM feature_flags WHERE id = 1"));
        Assert.Equal(1, QuerySqlite<long>("SELECT flag_a FROM feature_flags WHERE id = 1"));

        Assert.False(QueryPg<bool>("SELECT flag_b FROM feature_flags WHERE id = 1"));
        Assert.Equal(0, QuerySqlite<long>("SELECT flag_b FROM feature_flags WHERE id = 1"));

        Assert.True(QueryPg<bool>("SELECT flag_c FROM feature_flags WHERE id = 1"));
        Assert.Equal(1, QuerySqlite<long>("SELECT flag_c FROM feature_flags WHERE id = 1"));

        Assert.False(QueryPg<bool>("SELECT flag_d FROM feature_flags WHERE id = 1"));
        Assert.Equal(0, QuerySqlite<long>("SELECT flag_d FROM feature_flags WHERE id = 1"));
    }

    [Fact]
    public void LqlUuid_MultipleInserts_AllUnique_BothPlatforms()
    {
        // Arrange - Verify UUID generation is truly unique across many inserts
        var schema = Schema
            .Define("Test")
            .Table(
                "public",
                "uuid_test",
                t =>
                    t.Column("id", PortableTypes.Uuid, c => c.PrimaryKey().DefaultLql("gen_uuid()"))
                        .Column("seq", PortableTypes.Int, c => c.NotNull())
            )
            .Build();

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
        using var pgCmd = _pgConnection.CreateCommand();
        pgCmd.CommandText = "SELECT COUNT(DISTINCT id) FROM uuid_test";
        var pgDistinct = (long)pgCmd.ExecuteScalar()!;
        Assert.Equal(10, pgDistinct);

        // Assert - All UUIDs are unique in SQLite
        using var sqliteCmd = _sqliteConnection.CreateCommand();
        sqliteCmd.CommandText = "SELECT COUNT(DISTINCT id) FROM uuid_test";
        var sqliteDistinct = (long)sqliteCmd.ExecuteScalar()!;
        Assert.Equal(10, sqliteDistinct);
    }

    [Fact]
    public void LqlTimestamp_AllTimeTypes_WorkBothPlatforms()
    {
        // Arrange - Test all time-related LQL functions
        var schema = Schema
            .Define("Test")
            .Table(
                "public",
                "time_test",
                t =>
                    t.Column("id", PortableTypes.Int, c => c.PrimaryKey())
                        .Column("ts1", PortableTypes.DateTimeOffset, c => c.DefaultLql("now()"))
                        .Column(
                            "ts2",
                            PortableTypes.DateTimeOffset,
                            c => c.DefaultLql("current_timestamp()")
                        )
                        .Column("d", PortableTypes.Date, c => c.DefaultLql("current_date()"))
                        .Column("t", PortableTypes.Time(), c => c.DefaultLql("current_time()"))
            )
            .Build();

        // Act
        ApplySchema(_pgConnection, schema, PostgresDdlGenerator.Generate);
        ApplySchema(_sqliteConnection, schema, SqliteDdlGenerator.Generate);

        ExecutePg("INSERT INTO time_test (id) VALUES (1)");
        ExecuteSqlite("INSERT INTO time_test (id) VALUES (1)");

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
        var currentSchema = (
            (SchemaResultOk)PostgresSchemaInspector.Inspect(conn, "public", _logger)
        ).Value;
        var operations = (
            (OperationsResultOk)SchemaDiff.Calculate(currentSchema, schema, logger: _logger)
        ).Value;
        _ = MigrationRunner.Apply(conn, operations, generator, MigrationOptions.Default, _logger);
    }

    private void ApplySchema(
        SqliteConnection conn,
        SchemaDefinition schema,
        Func<SchemaOperation, string> generator
    )
    {
        var currentSchema = ((SchemaResultOk)SqliteSchemaInspector.Inspect(conn, _logger)).Value;
        var operations = (
            (OperationsResultOk)SchemaDiff.Calculate(currentSchema, schema, logger: _logger)
        ).Value;
        _ = MigrationRunner.Apply(conn, operations, generator, MigrationOptions.Default, _logger);
    }

    private void ExecutePg(string sql)
    {
        using var cmd = _pgConnection.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    private void ExecuteSqlite(string sql)
    {
        using var cmd = _sqliteConnection.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    private T QueryPg<T>(string sql)
    {
        using var cmd = _pgConnection.CreateCommand();
        cmd.CommandText = sql;
        return (T)cmd.ExecuteScalar()!;
    }

    private T QuerySqlite<T>(string sql)
    {
        using var cmd = _sqliteConnection.CreateCommand();
        cmd.CommandText = sql;
        return (T)cmd.ExecuteScalar()!;
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
        cmd.CommandText = """
            SELECT
                c.column_name,
                c.data_type,
                c.is_nullable,
                c.column_default,
                c.character_maximum_length,
                c.numeric_precision,
                c.numeric_scale
            FROM information_schema.columns c
            WHERE c.table_schema = @schema AND c.table_name = @table
            ORDER BY c.ordinal_position
            """;
        cmd.Parameters.AddWithValue("@schema", schema);
        cmd.Parameters.AddWithValue("@table", tableName);

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
        pkCmd.CommandText = """
            SELECT kcu.column_name
            FROM information_schema.table_constraints tc
            JOIN information_schema.key_column_usage kcu
                ON tc.constraint_name = kcu.constraint_name
                AND tc.table_schema = kcu.table_schema
            WHERE tc.table_schema = @schema
                AND tc.table_name = @table
                AND tc.constraint_type = 'PRIMARY KEY'
            ORDER BY kcu.ordinal_position
            """;
        pkCmd.Parameters.AddWithValue("@schema", schema);
        pkCmd.Parameters.AddWithValue("@table", tableName);

        var pkColumns = new List<string>();
        using var pkReader = pkCmd.ExecuteReader();
        while (pkReader.Read())
        {
            pkColumns.Add(pkReader.GetString(0));
        }

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
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT sql FROM sqlite_master WHERE type='table' AND name=@name";
        cmd.Parameters.AddWithValue("@name", tableName);
        var result = cmd.ExecuteScalar();
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
        cmd.CommandText = """
            SELECT column_name
            FROM information_schema.columns
            WHERE table_schema = @schema AND table_name = @table
            ORDER BY ordinal_position
            """;
        cmd.Parameters.AddWithValue("@schema", schema);
        cmd.Parameters.AddWithValue("@table", tableName);

        var columns = new List<string>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            columns.Add(reader.GetString(0));
        }
        return columns;
    }

    /// <summary>
    /// Get list of column names for a SQLite table.
    /// </summary>
    private static List<string> GetSqliteColumns(SqliteConnection conn, string tableName)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"PRAGMA table_info({tableName})";

        var columns = new List<string>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            // Column name is at index 1 in pragma table_info
            columns.Add(reader.GetString(1));
        }
        return columns;
    }
}
