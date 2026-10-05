namespace Nimblesite.DataProvider.Migration.Tests;

/// <summary>
/// Tests for PostgresDdlGenerator.MigrateSchema() method.
/// Covers: drop schema, fresh migration, partial upgrade scenarios.
/// Uses the shared postgres container; each test gets its own database.
/// </summary>
[Collection(PostgresTestSuite.Name)]
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Usage",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "Disposed via IAsyncLifetime.DisposeAsync"
)]
public sealed class MigrateSchemaTests(PostgresContainerFixture fixture) : IAsyncLifetime
{
    private NpgsqlConnection _connection = null!;

    public async Task InitializeAsync()
    {
        _connection = await fixture
            .CreateDatabaseAsync("migrate_schema_test")
            .ConfigureAwait(false);
    }

    public async Task DisposeAsync()
    {
        await _connection.DisposeAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Test schema definition for migration tests.
    /// </summary>
    private static SchemaDefinition CreateTestSchema() =>
        Schema
            .Define("MigrateSchemaTest")
            .Table(
                "public",
                "countries",
                t =>
                    t.Column(
                            "id",
                            PortableTypes.Uuid,
                            c => c.PrimaryKey().Default("gen_random_uuid()")
                        )
                        .Column("name", PortableTypes.VarChar(100), c => c.NotNull())
                        .Column("code", PortableTypes.VarChar(10), c => c.NotNull())
                        .Unique("uq_countries_code", "code")
            )
            .Table(
                "public",
                "regions",
                t =>
                    t.Column(
                            "id",
                            PortableTypes.Uuid,
                            c => c.PrimaryKey().Default("gen_random_uuid()")
                        )
                        .Column("name", PortableTypes.VarChar(100), c => c.NotNull())
                        .Column("country_id", PortableTypes.Uuid, c => c.NotNull())
                        .ForeignKey("country_id", "countries", "id", ForeignKeyAction.Cascade)
                        .Index("idx_regions_country", "country_id")
            )
            .Table(
                "public",
                "suburbs",
                t =>
                    t.Column(
                            "id",
                            PortableTypes.Uuid,
                            c => c.PrimaryKey().Default("gen_random_uuid()")
                        )
                        .Column("name", PortableTypes.VarChar(100), c => c.NotNull())
                        .Column("region_id", PortableTypes.Uuid, c => c.NotNull())
                        .ForeignKey("region_id", "regions", "id", ForeignKeyAction.Cascade)
            )
            .Table(
                "public",
                "venues",
                t =>
                    t.Column(
                            "id",
                            PortableTypes.Uuid,
                            c => c.PrimaryKey().Default("gen_random_uuid()")
                        )
                        .Column("name", PortableTypes.VarChar(200), c => c.NotNull())
                        .Column("suburb_id", PortableTypes.Uuid, c => c.NotNull())
                        .Column("address", PortableTypes.VarChar(300))
                        .ForeignKey("suburb_id", "suburbs", "id", ForeignKeyAction.Cascade)
                        .Unique("uq_venues_name_suburb", "name", "suburb_id")
            )
            .Build();

    [Fact]
    public void MigrateSchema_FreshDatabase_CreatesAllTables()
    {
        // Arrange
        var schema = CreateTestSchema();
        var tablesCreated = new List<string>();

        // Act
        var result = PostgresDdlGenerator.MigrateSchema(
            _connection,
            schema,
            onTableCreated: tablesCreated.Add
        );

        // Assert
        Assert.True(
            result.Success,
            $"Migration failed with errors: {string.Join(", ", result.Errors)}"
        );
        Assert.Equal(4, result.TablesCreated);
        Assert.Empty(result.Errors);
        Assert.Contains("countries", tablesCreated);
        Assert.Contains("regions", tablesCreated);
        Assert.Contains("suburbs", tablesCreated);
        Assert.Contains("venues", tablesCreated);

        // Verify tables exist in database
        AssertTestTablesExist();
    }

    [Fact]
    public void MigrateSchema_AlreadyMigrated_IsIdempotent()
    {
        // Arrange
        var schema = CreateTestSchema();

        // First migration
        var firstResult = PostgresDdlGenerator.MigrateSchema(_connection, schema);
        Assert.True(firstResult.Success);

        // Act - Run migration again
        var secondResult = PostgresDdlGenerator.MigrateSchema(_connection, schema);

        // Assert - Should succeed without errors (CREATE TABLE IF NOT EXISTS)
        Assert.True(secondResult.Success);
        Assert.Equal(4, secondResult.TablesCreated);
        Assert.Empty(secondResult.Errors);
    }

    [Fact]
    public void MigrateSchema_PartiallyMigrated_CreatesRemainingTables()
    {
        // Arrange - Create only the first two tables manually
        ExecuteSql(
            """
            CREATE TABLE IF NOT EXISTS "public"."countries" (
                "id" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
                "name" VARCHAR(100) NOT NULL,
                "code" VARCHAR(10) NOT NULL,
                CONSTRAINT "uq_countries_code" UNIQUE ("code")
            )
            """
        );

        ExecuteSql(
            """
            CREATE TABLE IF NOT EXISTS "public"."regions" (
                "id" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
                "name" VARCHAR(100) NOT NULL,
                "country_id" UUID NOT NULL,
                CONSTRAINT "fk_regions_country" FOREIGN KEY ("country_id")
                    REFERENCES "public"."countries" ("id") ON DELETE CASCADE
            )
            """
        );

        Assert.True(TableExists("countries"));
        Assert.True(TableExists("regions"));
        Assert.False(TableExists("suburbs"));
        Assert.False(TableExists("venues"));

        var schema = CreateTestSchema();
        var tablesCreated = new List<string>();

        // Act - Run migration on partially migrated database
        var result = PostgresDdlGenerator.MigrateSchema(
            _connection,
            schema,
            onTableCreated: tablesCreated.Add
        );

        // Assert
        Assert.True(result.Success, $"Migration failed: {string.Join(", ", result.Errors)}");
        Assert.Equal(4, result.TablesCreated); // All 4 reported (IF NOT EXISTS)
        Assert.Empty(result.Errors);

        // All tables should now exist
        AssertTestTablesExist();
    }

    [Fact]
    public void MigrateSchema_TableCreationFails_ContinuesWithOtherTables()
    {
        // Arrange - Create a conflicting table that will cause FK failure
        // Create suburbs without its parent (regions) - this will cause venues to fail FK
        ExecuteSql(
            """
            CREATE TABLE IF NOT EXISTS "public"."orphan_suburbs" (
                "id" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
                "name" VARCHAR(100) NOT NULL
            )
            """
        );

        // Create a schema where one table references a non-existent table
        var schemaWithBadFk = Schema
            .Define("Test")
            .Table(
                "public",
                "good_table",
                t => t.Column("id", PortableTypes.Uuid, c => c.PrimaryKey())
            )
            .Table(
                "public",
                "bad_table",
                t =>
                    t.Column("id", PortableTypes.Uuid, c => c.PrimaryKey())
                        .Column("ref_id", PortableTypes.Uuid)
                        .ForeignKey("ref_id", "nonexistent_table", "id")
            )
            .Table(
                "public",
                "another_good_table",
                t => t.Column("id", PortableTypes.Uuid, c => c.PrimaryKey())
            )
            .Build();

        var failedTables = new List<string>();

        // Act
        var result = PostgresDdlGenerator.MigrateSchema(
            _connection,
            schemaWithBadFk,
            onTableFailed: (name, _) => failedTables.Add(name)
        );

        // Assert - Should have created 2 tables, failed on 1
        Assert.False(result.Success);
        Assert.Equal(2, result.TablesCreated);
        Assert.Single(result.Errors);
        Assert.Contains("bad_table", failedTables);
        Assert.True(TableExists("good_table"));
        Assert.True(TableExists("another_good_table"));
        Assert.False(TableExists("bad_table"));
    }

    [Fact]
    public void MigrateSchema_CallbacksInvoked_ForSuccessAndFailure()
    {
        // Arrange
        var schemaWithBadTable = Schema
            .Define("Test")
            .Table(
                "public",
                "success_table",
                t => t.Column("id", PortableTypes.Uuid, c => c.PrimaryKey())
            )
            .Table(
                "public",
                "fail_table",
                t =>
                    t.Column("id", PortableTypes.Uuid, c => c.PrimaryKey())
                        .ForeignKey("id", "missing_parent", "id")
            )
            .Build();

        var createdTables = new List<string>();
        var failedTables = new List<(string Name, Exception Ex)>();

        // Act
        var result = PostgresDdlGenerator.MigrateSchema(
            _connection,
            schemaWithBadTable,
            onTableCreated: createdTables.Add,
            onTableFailed: (name, ex) => failedTables.Add((name, ex))
        );

        // Assert
        Assert.False(result.Success);
        Assert.Single(createdTables);
        Assert.Equal("success_table", createdTables[0]);
        Assert.Single(failedTables);
        Assert.Equal("fail_table", failedTables[0].Name);
        Assert.NotNull(failedTables[0].Ex);
    }

    [Fact]
    public void DropAllTables_RemovesAllTablesInReverseOrder()
    {
        // Arrange - Create schema first
        var schema = CreateTestSchema();
        var migrateResult = PostgresDdlGenerator.MigrateSchema(_connection, schema);
        Assert.True(migrateResult.Success);
        Assert.True(TableExists("countries"));
        Assert.True(TableExists("venues"));

        // Act - Drop all tables in reverse order (respecting FK constraints)
        var tables = schema.Tables.Reverse().ToList();
        foreach (var table in tables)
        {
            ExecuteSql($"DROP TABLE IF EXISTS \"{table.Schema}\".\"{table.Name}\" CASCADE");
        }

        // Assert
        Assert.False(TableExists("countries"));
        Assert.False(TableExists("regions"));
        Assert.False(TableExists("suburbs"));
        Assert.False(TableExists("venues"));
    }

    [Fact]
    public void MigrateSchema_AfterDrop_RecreatesAllTables()
    {
        // Arrange - Create and drop schema
        var schema = CreateTestSchema();
        _ = PostgresDdlGenerator.MigrateSchema(_connection, schema);

        // Drop all tables
        var tables = schema.Tables.Reverse().ToList();
        foreach (var table in tables)
        {
            ExecuteSql($"DROP TABLE IF EXISTS \"{table.Schema}\".\"{table.Name}\" CASCADE");
        }

        Assert.False(TableExists("countries"));

        // Act - Migrate again
        var result = PostgresDdlGenerator.MigrateSchema(_connection, schema);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(4, result.TablesCreated);
        AssertTestTablesExist();
    }

    [Fact]
    public void MigrateSchema_WithIndexes_CreatesIndexes()
    {
        // Arrange
        var schema = CreateTestSchema();

        // Act
        var result = PostgresDdlGenerator.MigrateSchema(_connection, schema);

        // Assert
        Assert.True(result.Success);
        Assert.True(IndexExists("idx_regions_country"));
    }

    [Fact]
    public void MigrateSchema_EmptySchema_ReturnsSuccess()
    {
        // Arrange
        var emptySchema = Schema.Define("Empty").Build();

        // Act
        var result = PostgresDdlGenerator.MigrateSchema(_connection, emptySchema);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(0, result.TablesCreated);
        Assert.Empty(result.Errors);
    }

    // Implements [MIG-TEST-TABLE-ASSERTIONS].
    private void AssertTestTablesExist()
    {
        Assert.True(TableExists("countries"));
        Assert.True(TableExists("regions"));
        Assert.True(TableExists("suburbs"));
        Assert.True(TableExists("venues"));
    }

    private bool TableExists(string tableName)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            SELECT EXISTS (
                SELECT FROM information_schema.tables
                WHERE table_schema = 'public'
                AND table_name = @tableName
            )
            """;
        var param = cmd.CreateParameter();
        param.ParameterName = "@tableName";
        param.Value = tableName;
        cmd.Parameters.Add(param);
        return (bool)cmd.ExecuteScalar()!;
    }

    private bool IndexExists(string indexName)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            SELECT EXISTS (
                SELECT FROM pg_indexes
                WHERE schemaname = 'public'
                AND indexname = @indexName
            )
            """;
        var param = cmd.CreateParameter();
        param.ParameterName = "@indexName";
        param.Value = indexName;
        cmd.Parameters.Add(param);
        return (bool)cmd.ExecuteScalar()!;
    }

    private void ExecuteSql(string sql)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }
}
