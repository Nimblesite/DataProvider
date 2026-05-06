namespace Nimblesite.DataProvider.Migration.Tests;

/// <summary>
/// E2E tests for PostgreSQL migrations using a shared Testcontainers postgres.
/// Each test gets its own database within the shared container.
/// </summary>
[Collection(PostgresTestSuite.Name)]
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Usage",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "Disposed via IAsyncLifetime.DisposeAsync"
)]
public sealed class PostgresMigrationTests(PostgresContainerFixture fixture) : IAsyncLifetime
{
    private NpgsqlConnection _connection = null!;
    private readonly ILogger _logger = NullLogger.Instance;

    public async Task InitializeAsync()
    {
        _connection = await fixture.CreateDatabaseAsync("migration_test").ConfigureAwait(false);
    }

    public async Task DisposeAsync()
    {
        await _connection.DisposeAsync().ConfigureAwait(false);
    }

    [Fact]
    public void CreateDatabaseFromScratch_SingleTable_Success()
    {
        // Arrange
        var schema = Schema
            .Define("Test")
            .Table(
                "public",
                "users",
                t =>
                    t.Column("id", PortableTypes.Uuid, c => c.PrimaryKey())
                        .Column("email", PortableTypes.VarChar(255), c => c.NotNull())
                        .Column("name", PortableTypes.VarChar(100))
                        .Index("idx_users_email", "email", unique: true)
            )
            .Build();

        // Act
        var emptySchema = PostgresSchemaInspector.Inspect(_connection, "public", _logger);
        Assert.True(emptySchema is SchemaResultOk);

        var operations = SchemaDiff.Calculate(
            ((SchemaResultOk)emptySchema).Value,
            schema,
            logger: _logger
        );
        Assert.True(operations is OperationsResultOk);

        var ops = ((OperationsResultOk)operations).Value;

        var result = MigrationRunner.Apply(
            _connection,
            ops,
            PostgresDdlGenerator.Generate,
            MigrationOptions.Default,
            _logger
        );

        // Assert
        Assert.True(
            result is MigrationApplyResultOk,
            $"Migration failed: {(result as MigrationApplyResultError)?.Value}"
        );

        // Verify table exists
        var inspected = PostgresSchemaInspector.Inspect(_connection, "public", _logger);
        Assert.True(inspected is SchemaResultOk);
        var inspectedSchema = ((SchemaResultOk)inspected).Value;
        Assert.Contains(inspectedSchema.Tables, t => t.Name == "users");
    }

    [Fact]
    public void CreateDatabaseFromScratch_MultipleTablesWithForeignKeys_Success()
    {
        // Arrange
        var schema = Schema
            .Define("Test")
            .Table(
                "public",
                "customers",
                t =>
                    t.Column("id", PortableTypes.Uuid, c => c.PrimaryKey())
                        .Column("email", PortableTypes.VarChar(255), c => c.NotNull())
            )
            .Table(
                "public",
                "invoices",
                t =>
                    t.Column("id", PortableTypes.BigInt, c => c.PrimaryKey().Identity())
                        .Column("customer_id", PortableTypes.Uuid, c => c.NotNull())
                        .Column("total", PortableTypes.Decimal(12, 2), c => c.NotNull())
                        .Column(
                            "created_at",
                            PortableTypes.DateTimeOffset,
                            c => c.NotNull().Default("CURRENT_TIMESTAMP")
                        )
                        .ForeignKey("customer_id", "customers", "id", ForeignKeyAction.Cascade)
            )
            .Build();

        // Act
        var emptySchema = (
            (SchemaResultOk)PostgresSchemaInspector.Inspect(_connection, "public", _logger)
        ).Value;

        var operations = (
            (OperationsResultOk)SchemaDiff.Calculate(emptySchema, schema, logger: _logger)
        ).Value;

        var result = MigrationRunner.Apply(
            _connection,
            operations,
            PostgresDdlGenerator.Generate,
            MigrationOptions.Default,
            _logger
        );

        // Assert
        Assert.True(
            result is MigrationApplyResultOk,
            $"Migration failed: {(result as MigrationApplyResultError)?.Value}"
        );

        var inspected = (
            (SchemaResultOk)PostgresSchemaInspector.Inspect(_connection, "public", _logger)
        ).Value;

        Assert.Contains(inspected.Tables, t => t.Name == "customers");
        Assert.Contains(inspected.Tables, t => t.Name == "invoices");

        var invoicesTable = inspected.Tables.First(t => t.Name == "invoices");
        Assert.NotEmpty(invoicesTable.ForeignKeys);
    }

    [Fact]
    public void UpgradeExistingDatabase_AddColumn_Success()
    {
        // Arrange - Create initial schema
        var v1 = Schema
            .Define("Test")
            .Table(
                "public",
                "products",
                t =>
                    t.Column("id", PortableTypes.Uuid, c => c.PrimaryKey())
                        .Column("name", PortableTypes.VarChar(200))
            )
            .Build();

        var emptySchema = (
            (SchemaResultOk)PostgresSchemaInspector.Inspect(_connection, "public", _logger)
        ).Value;
        var v1Ops = (
            (OperationsResultOk)SchemaDiff.Calculate(emptySchema, v1, logger: _logger)
        ).Value;
        _ = MigrationRunner.Apply(
            _connection,
            v1Ops,
            PostgresDdlGenerator.Generate,
            MigrationOptions.Default,
            _logger
        );

        // Define v2 with new columns
        var v2 = Schema
            .Define("Test")
            .Table(
                "public",
                "products",
                t =>
                    t.Column("id", PortableTypes.Uuid, c => c.PrimaryKey())
                        .Column("name", PortableTypes.VarChar(200))
                        .Column("price", PortableTypes.Decimal(10, 2))
                        .Column("sku", PortableTypes.VarChar(50))
            )
            .Build();

        // Act
        var currentSchema = (
            (SchemaResultOk)PostgresSchemaInspector.Inspect(_connection, "public", _logger)
        ).Value;

        var upgradeOps = (
            (OperationsResultOk)SchemaDiff.Calculate(currentSchema, v2, logger: _logger)
        ).Value;

        // Should have 2 AddColumn operations
        Assert.Equal(2, upgradeOps.Count);
        Assert.All(upgradeOps, op => Assert.IsType<AddColumnOperation>(op));

        var result = MigrationRunner.Apply(
            _connection,
            upgradeOps,
            PostgresDdlGenerator.Generate,
            MigrationOptions.Default,
            _logger
        );

        // Assert
        Assert.True(result is MigrationApplyResultOk);

        var finalSchema = (
            (SchemaResultOk)PostgresSchemaInspector.Inspect(_connection, "public", _logger)
        ).Value;
        var products = finalSchema.Tables.Single(t => t.Name == "products");
        Assert.Equal(4, products.Columns.Count);
    }

    [Fact]
    public void UpgradeExistingDatabase_AddTable_Success()
    {
        // Arrange
        var v1 = Schema
            .Define("Test")
            .Table(
                "public",
                "categories",
                t =>
                    t.Column("id", PortableTypes.Int, c => c.PrimaryKey().Identity())
                        .Column("name", PortableTypes.VarChar(100))
            )
            .Build();

        var emptySchema = (
            (SchemaResultOk)PostgresSchemaInspector.Inspect(_connection, "public", _logger)
        ).Value;
        var v1Ops = (
            (OperationsResultOk)SchemaDiff.Calculate(emptySchema, v1, logger: _logger)
        ).Value;
        _ = MigrationRunner.Apply(
            _connection,
            v1Ops,
            PostgresDdlGenerator.Generate,
            MigrationOptions.Default,
            _logger
        );

        // v2 adds a new table
        var v2 = Schema
            .Define("Test")
            .Table(
                "public",
                "categories",
                t =>
                    t.Column("id", PortableTypes.Int, c => c.PrimaryKey().Identity())
                        .Column("name", PortableTypes.VarChar(100))
            )
            .Table(
                "public",
                "items",
                t =>
                    t.Column("id", PortableTypes.BigInt, c => c.PrimaryKey().Identity())
                        .Column("category_id", PortableTypes.Int, c => c.NotNull())
                        .Column("title", PortableTypes.VarChar(300), c => c.NotNull())
                        .ForeignKey("category_id", "categories", "id")
            )
            .Build();

        // Act
        var currentSchema = (
            (SchemaResultOk)PostgresSchemaInspector.Inspect(_connection, "public", _logger)
        ).Value;

        var upgradeOps = (
            (OperationsResultOk)SchemaDiff.Calculate(currentSchema, v2, logger: _logger)
        ).Value;

        Assert.Single(upgradeOps);
        Assert.IsType<CreateTableOperation>(upgradeOps[0]);

        var result = MigrationRunner.Apply(
            _connection,
            upgradeOps,
            PostgresDdlGenerator.Generate,
            MigrationOptions.Default,
            _logger
        );

        // Assert
        Assert.True(result is MigrationApplyResultOk);

        var finalSchema = (
            (SchemaResultOk)PostgresSchemaInspector.Inspect(_connection, "public", _logger)
        ).Value;
        Assert.Contains(finalSchema.Tables, t => t.Name == "items");
    }

    [Fact]
    public void UpgradeExistingDatabase_AddIndex_Success()
    {
        // Arrange
        var v1 = Schema
            .Define("Test")
            .Table(
                "public",
                "logs",
                t =>
                    t.Column("id", PortableTypes.BigInt, c => c.PrimaryKey().Identity())
                        .Column("message", PortableTypes.Text)
                        .Column("level", PortableTypes.VarChar(20))
            )
            .Build();

        var emptySchema = (
            (SchemaResultOk)PostgresSchemaInspector.Inspect(_connection, "public", _logger)
        ).Value;
        var v1Ops = (
            (OperationsResultOk)SchemaDiff.Calculate(emptySchema, v1, logger: _logger)
        ).Value;
        _ = MigrationRunner.Apply(
            _connection,
            v1Ops,
            PostgresDdlGenerator.Generate,
            MigrationOptions.Default,
            _logger
        );

        // v2 adds an index
        var v2 = Schema
            .Define("Test")
            .Table(
                "public",
                "logs",
                t =>
                    t.Column("id", PortableTypes.BigInt, c => c.PrimaryKey().Identity())
                        .Column("message", PortableTypes.Text)
                        .Column("level", PortableTypes.VarChar(20))
                        .Index("idx_logs_level", "level")
            )
            .Build();

        // Act
        var currentSchema = (
            (SchemaResultOk)PostgresSchemaInspector.Inspect(_connection, "public", _logger)
        ).Value;

        var upgradeOps = (
            (OperationsResultOk)SchemaDiff.Calculate(currentSchema, v2, logger: _logger)
        ).Value;

        Assert.Single(upgradeOps);
        Assert.IsType<CreateIndexOperation>(upgradeOps[0]);

        var result = MigrationRunner.Apply(
            _connection,
            upgradeOps,
            PostgresDdlGenerator.Generate,
            MigrationOptions.Default,
            _logger
        );

        // Assert
        Assert.True(result is MigrationApplyResultOk);

        var finalSchema = (
            (SchemaResultOk)PostgresSchemaInspector.Inspect(_connection, "public", _logger)
        ).Value;
        var logs = finalSchema.Tables.Single(t => t.Name == "logs");
        Assert.Contains(logs.Indexes, i => i.Name == "idx_logs_level");
    }

    [Fact]
    public void Migration_IsIdempotent_NoErrorOnRerun()
    {
        // Arrange
        var schema = Schema
            .Define("Test")
            .Table(
                "public",
                "settings",
                t =>
                    t.Column("key", PortableTypes.VarChar(100), c => c.PrimaryKey())
                        .Column("value", PortableTypes.Text)
            )
            .Build();

        // Act - Run migration twice
        for (var i = 0; i < 2; i++)
        {
            var currentSchema = (
                (SchemaResultOk)PostgresSchemaInspector.Inspect(_connection, "public", _logger)
            ).Value;

            var operations = (
                (OperationsResultOk)SchemaDiff.Calculate(currentSchema, schema, logger: _logger)
            ).Value;

            var result = MigrationRunner.Apply(
                _connection,
                operations,
                PostgresDdlGenerator.Generate,
                MigrationOptions.Default,
                _logger
            );

            Assert.True(result is MigrationApplyResultOk);

            // Second run should have 0 operations
            if (i == 1)
            {
                Assert.Empty(operations);
            }
        }
    }

    [Fact]
    public void CreateTable_PostgresNativeTypes_Success()
    {
        // Arrange
        var schema = Schema
            .Define("Test")
            .Table(
                "public",
                "type_test",
                t =>
                    t.Column("id", PortableTypes.BigInt, c => c.PrimaryKey())
                        .Column("uuid_col", PortableTypes.Uuid)
                        .Column("json_col", PortableTypes.Json)
                        .Column("bool_col", PortableTypes.Boolean)
                        .Column("timestamp_col", PortableTypes.DateTimeOffset)
                        .Column("decimal_col", PortableTypes.Decimal(18, 4))
            )
            .Build();

        // Act
        var emptySchema = (
            (SchemaResultOk)PostgresSchemaInspector.Inspect(_connection, "public", _logger)
        ).Value;
        var operations = (
            (OperationsResultOk)SchemaDiff.Calculate(emptySchema, schema, logger: _logger)
        ).Value;

        var result = MigrationRunner.Apply(
            _connection,
            operations,
            PostgresDdlGenerator.Generate,
            MigrationOptions.Default,
            _logger
        );

        // Assert
        Assert.True(result is MigrationApplyResultOk);

        // Verify native types are used
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            SELECT column_name, data_type
            FROM information_schema.columns
            WHERE table_name = 'type_test'
            """;
        using var reader = cmd.ExecuteReader();

        var columns = new Dictionary<string, string>();
        while (reader.Read())
        {
            columns[reader.GetString(0)] = reader.GetString(1);
        }

        Assert.Equal("uuid", columns["uuid_col"]);
        Assert.Equal("jsonb", columns["json_col"]);
        Assert.Equal("boolean", columns["bool_col"]);
        Assert.Contains("timestamp", columns["timestamp_col"]);
    }

    [Fact]
    public void CreateTable_MixedCaseColumnCheckConstraint_PreservesIdentifierCase()
    {
        var schema = Schema
            .Define("Test")
            .Table(
                "public",
                "fhir_patient",
                t =>
                    t.Column("id", PortableTypes.Uuid, c => c.PrimaryKey())
                        .Column(
                            "Gender",
                            PortableTypes.Text,
                            c => c.NotNull().Check("\"Gender\" IN ('male', 'female', 'other')")
                        )
            )
            .Build();

        var current = (
            (SchemaResultOk)PostgresSchemaInspector.Inspect(_connection, "public", _logger)
        ).Value;
        var operations = (
            (OperationsResultOk)SchemaDiff.Calculate(current, schema, logger: _logger)
        ).Value;

        var result = MigrationRunner.Apply(
            _connection,
            operations,
            PostgresDdlGenerator.Generate,
            MigrationOptions.Default,
            _logger
        );

        Assert.True(
            result is MigrationApplyResultOk,
            $"Migration failed: {(result as MigrationApplyResultError)?.Value}"
        );
        InsertPatientGender("male");
        var ex = Assert.Throws<PostgresException>(() => InsertPatientGender("invalid"));
        Assert.Equal("23514", ex.SqlState);
    }

    [Fact]
    public void ExpressionIndex_CreateWithLowerFunction_Success()
    {
        // Arrange - Create table with expression index for case-insensitive uniqueness
        var schema = Schema
            .Define("Test")
            .Table(
                "public",
                "artists",
                t =>
                    t.Column("id", PortableTypes.Uuid, c => c.PrimaryKey())
                        .Column("name", PortableTypes.VarChar(200), c => c.NotNull())
                        .ExpressionIndex("uq_artists_name", "lower(name)", unique: true)
            )
            .Build();

        // Act
        var emptySchema = (
            (SchemaResultOk)PostgresSchemaInspector.Inspect(_connection, "public", _logger)
        ).Value;
        var operations = (
            (OperationsResultOk)SchemaDiff.Calculate(emptySchema, schema, logger: _logger)
        ).Value;

        var result = MigrationRunner.Apply(
            _connection,
            operations,
            PostgresDdlGenerator.Generate,
            MigrationOptions.Default,
            _logger
        );

        // Assert - Migration succeeded
        Assert.True(
            result is MigrationApplyResultOk,
            $"Migration failed: {(result as MigrationApplyResultError)?.Value}"
        );

        // Verify index exists and is unique
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            SELECT indexname, indexdef
            FROM pg_indexes
            WHERE tablename = 'artists' AND indexname = 'uq_artists_name'
            """;
        using var reader = cmd.ExecuteReader();

        Assert.True(reader.Read(), "Expression index should exist");
        var indexDef = reader.GetString(1);
        Assert.Contains("UNIQUE", indexDef);
        Assert.Contains("lower", indexDef);
    }

    [Fact]
    public void ExpressionIndex_EnforcesCaseInsensitiveUniqueness()
    {
        // Arrange - Create table with expression index
        var schema = Schema
            .Define("Test")
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
                        .ExpressionIndex("uq_venues_name", "lower(name)", unique: true)
            )
            .Build();

        var emptySchema = (
            (SchemaResultOk)PostgresSchemaInspector.Inspect(_connection, "public", _logger)
        ).Value;
        var operations = (
            (OperationsResultOk)SchemaDiff.Calculate(emptySchema, schema, logger: _logger)
        ).Value;
        _ = MigrationRunner.Apply(
            _connection,
            operations,
            PostgresDdlGenerator.Generate,
            MigrationOptions.Default,
            _logger
        );

        // Act - Insert first venue
        using var insertCmd = _connection.CreateCommand();
        insertCmd.CommandText = "INSERT INTO venues (name) VALUES ('The Corner Hotel')";
        insertCmd.ExecuteNonQuery();

        // Try to insert duplicate with different case - should fail
        using var duplicateCmd = _connection.CreateCommand();
        duplicateCmd.CommandText = "INSERT INTO venues (name) VALUES ('THE CORNER HOTEL')";

        // Assert - Should throw unique constraint violation
        var ex = Assert.Throws<PostgresException>(() => duplicateCmd.ExecuteNonQuery());
        Assert.Contains("uq_venues_name", ex.Message);
    }

    [Fact]
    public void ExpressionIndex_MultiExpression_CompositeIndexSuccess()
    {
        // Arrange - Create table with multi-expression index (like venues with suburb_id)
        var schema = Schema
            .Define("Test")
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
            )
            .Table(
                "public",
                "places",
                t =>
                    t.Column(
                            "id",
                            PortableTypes.Uuid,
                            c => c.PrimaryKey().Default("gen_random_uuid()")
                        )
                        .Column("name", PortableTypes.VarChar(200), c => c.NotNull())
                        .Column("suburb_id", PortableTypes.Uuid, c => c.NotNull())
                        .ForeignKey("suburb_id", "suburbs", "id")
                        .ExpressionIndex(
                            "uq_places_name_suburb",
                            ["lower(name)", "suburb_id"],
                            unique: true
                        )
            )
            .Build();

        // Act
        var emptySchema = (
            (SchemaResultOk)PostgresSchemaInspector.Inspect(_connection, "public", _logger)
        ).Value;
        var operations = (
            (OperationsResultOk)SchemaDiff.Calculate(emptySchema, schema, logger: _logger)
        ).Value;

        var result = MigrationRunner.Apply(
            _connection,
            operations,
            PostgresDdlGenerator.Generate,
            MigrationOptions.Default,
            _logger
        );

        // Assert
        Assert.True(
            result is MigrationApplyResultOk,
            $"Migration failed: {(result as MigrationApplyResultError)?.Value}"
        );

        // Verify composite expression index exists
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            SELECT indexdef
            FROM pg_indexes
            WHERE tablename = 'places' AND indexname = 'uq_places_name_suburb'
            """;
        var indexDef = (string?)cmd.ExecuteScalar();

        Assert.NotNull(indexDef);
        Assert.Contains("UNIQUE", indexDef);
        Assert.Contains("lower", indexDef);
        Assert.Contains("suburb_id", indexDef);
    }

    [Fact]
    public void ExpressionIndex_MultiExpression_AllowsSameNameDifferentSuburb()
    {
        // Arrange - Create tables with composite expression index
        var schema = Schema
            .Define("Test")
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
            )
            .Table(
                "public",
                "locations",
                t =>
                    t.Column(
                            "id",
                            PortableTypes.Uuid,
                            c => c.PrimaryKey().Default("gen_random_uuid()")
                        )
                        .Column("name", PortableTypes.VarChar(200), c => c.NotNull())
                        .Column("region_id", PortableTypes.Uuid, c => c.NotNull())
                        .ForeignKey("region_id", "regions", "id")
                        .ExpressionIndex(
                            "uq_locations_name_region",
                            ["lower(name)", "region_id"],
                            unique: true
                        )
            )
            .Build();

        var emptySchema = (
            (SchemaResultOk)PostgresSchemaInspector.Inspect(_connection, "public", _logger)
        ).Value;
        var operations = (
            (OperationsResultOk)SchemaDiff.Calculate(emptySchema, schema, logger: _logger)
        ).Value;
        _ = MigrationRunner.Apply(
            _connection,
            operations,
            PostgresDdlGenerator.Generate,
            MigrationOptions.Default,
            _logger
        );

        // Create two regions
        using var regionCmd = _connection.CreateCommand();
        regionCmd.CommandText = """
            INSERT INTO regions (id, name) VALUES
            ('11111111-1111-1111-1111-111111111111', 'Melbourne'),
            ('22222222-2222-2222-2222-222222222222', 'Sydney')
            """;
        regionCmd.ExecuteNonQuery();

        // Act - Insert same name in different regions (should succeed)
        using var insertCmd = _connection.CreateCommand();
        insertCmd.CommandText = """
            INSERT INTO locations (name, region_id) VALUES
            ('The Corner', '11111111-1111-1111-1111-111111111111'),
            ('The Corner', '22222222-2222-2222-2222-222222222222')
            """;
        var rowsAffected = insertCmd.ExecuteNonQuery();

        // Assert - Both inserts should succeed
        Assert.Equal(2, rowsAffected);

        // Try to insert duplicate in same region - should fail
        using var duplicateCmd = _connection.CreateCommand();
        duplicateCmd.CommandText = """
            INSERT INTO locations (name, region_id) VALUES
            ('THE CORNER', '11111111-1111-1111-1111-111111111111')
            """;

        var ex = Assert.Throws<PostgresException>(() => duplicateCmd.ExecuteNonQuery());
        Assert.Contains("uq_locations_name_region", ex.Message);
    }

    [Fact]
    public void ExpressionIndex_Idempotent_NoErrorOnRerun()
    {
        // Arrange
        var schema = Schema
            .Define("Test")
            .Table(
                "public",
                "bands",
                t =>
                    t.Column("id", PortableTypes.Uuid, c => c.PrimaryKey())
                        .Column("name", PortableTypes.VarChar(200), c => c.NotNull())
                        .ExpressionIndex("uq_bands_name", "lower(name)", unique: true)
            )
            .Build();

        // Act - Run migration twice
        // Note: Expression indexes aren't read back by schema inspector (no expression index introspection)
        // but CREATE INDEX IF NOT EXISTS ensures idempotency at the database level
        for (var i = 0; i < 2; i++)
        {
            var currentSchema = (
                (SchemaResultOk)PostgresSchemaInspector.Inspect(_connection, "public", _logger)
            ).Value;

            var operations = (
                (OperationsResultOk)SchemaDiff.Calculate(currentSchema, schema, logger: _logger)
            ).Value;

            var result = MigrationRunner.Apply(
                _connection,
                operations,
                PostgresDdlGenerator.Generate,
                MigrationOptions.Default,
                _logger
            );

            // Both runs should succeed - IF NOT EXISTS handles already-existing index
            Assert.True(
                result is MigrationApplyResultOk,
                $"Migration {i + 1} failed: {(result as MigrationApplyResultError)?.Value}"
            );
        }

        // Verify expression index exists and is functional
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            SELECT indexdef FROM pg_indexes
            WHERE tablename = 'bands' AND indexname = 'uq_bands_name'
            """;
        var indexDef = (string?)cmd.ExecuteScalar();
        Assert.NotNull(indexDef);
        Assert.Contains("lower", indexDef);
    }

    // =============================================================================
    // Index Conversion Tests (Column <-> Expression)
    // =============================================================================

    [Fact]
    public void UpgradeIndex_ColumnToExpression_RequiresDropAndCreate()
    {
        // Arrange - Create table with regular column index
        var v1 = Schema
            .Define("Test")
            .Table(
                "public",
                "artists",
                t =>
                    t.Column("id", PortableTypes.Uuid, c => c.PrimaryKey())
                        .Column("name", PortableTypes.VarChar(200), c => c.NotNull())
                        .Index("idx_artists_name", "name", unique: true)
            )
            .Build();

        // Apply v1
        var emptySchema = (
            (SchemaResultOk)PostgresSchemaInspector.Inspect(_connection, "public", _logger)
        ).Value;
        var v1Ops = (
            (OperationsResultOk)SchemaDiff.Calculate(emptySchema, v1, logger: _logger)
        ).Value;
        _ = MigrationRunner.Apply(
            _connection,
            v1Ops,
            PostgresDdlGenerator.Generate,
            MigrationOptions.Default,
            _logger
        );

        // v2 changes to expression index (different name since semantically different)
        var v2 = Schema
            .Define("Test")
            .Table(
                "public",
                "artists",
                t =>
                    t.Column("id", PortableTypes.Uuid, c => c.PrimaryKey())
                        .Column("name", PortableTypes.VarChar(200), c => c.NotNull())
                        .ExpressionIndex("uq_artists_name_ci", "lower(name)", unique: true)
            )
            .Build();

        // Act - Calculate upgrade operations
        var currentSchema = (
            (SchemaResultOk)PostgresSchemaInspector.Inspect(_connection, "public", _logger)
        ).Value;
        var upgradeOps = (
            (OperationsResultOk)
                SchemaDiff.Calculate(currentSchema, v2, allowDestructive: true, logger: _logger)
        ).Value;

        // Assert - Should have drop old index + create new expression index
        Assert.Equal(2, upgradeOps.Count);
        Assert.Contains(upgradeOps, op => op is DropIndexOperation);
        Assert.Contains(upgradeOps, op => op is CreateIndexOperation);

        // Apply the upgrade
        var result = MigrationRunner.Apply(
            _connection,
            upgradeOps,
            PostgresDdlGenerator.Generate,
            MigrationOptions.Destructive,
            _logger
        );

        Assert.True(
            result is MigrationApplyResultOk,
            $"Upgrade failed: {(result as MigrationApplyResultError)?.Value}"
        );

        // Verify new expression index exists
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            SELECT indexdef FROM pg_indexes
            WHERE tablename = 'artists' AND indexname = 'uq_artists_name_ci'
            """;
        var indexDef = (string?)cmd.ExecuteScalar();
        Assert.NotNull(indexDef);
        Assert.Contains("lower", indexDef);
    }

    [Fact]
    public void UpgradeIndex_ExpressionToColumn_RequiresDropAndCreate()
    {
        // Arrange - Create table with expression index
        var v1 = Schema
            .Define("Test")
            .Table(
                "public",
                "venues",
                t =>
                    t.Column("id", PortableTypes.Uuid, c => c.PrimaryKey())
                        .Column("name", PortableTypes.VarChar(200), c => c.NotNull())
                        .ExpressionIndex("uq_venues_name", "lower(name)", unique: true)
            )
            .Build();

        // Apply v1
        var emptySchema = (
            (SchemaResultOk)PostgresSchemaInspector.Inspect(_connection, "public", _logger)
        ).Value;
        var v1Ops = (
            (OperationsResultOk)SchemaDiff.Calculate(emptySchema, v1, logger: _logger)
        ).Value;
        _ = MigrationRunner.Apply(
            _connection,
            v1Ops,
            PostgresDdlGenerator.Generate,
            MigrationOptions.Default,
            _logger
        );

        // v2 changes back to simple column index (different name)
        var v2 = Schema
            .Define("Test")
            .Table(
                "public",
                "venues",
                t =>
                    t.Column("id", PortableTypes.Uuid, c => c.PrimaryKey())
                        .Column("name", PortableTypes.VarChar(200), c => c.NotNull())
                        .Index("idx_venues_name", "name", unique: true)
            )
            .Build();

        // Act
        var currentSchema = (
            (SchemaResultOk)PostgresSchemaInspector.Inspect(_connection, "public", _logger)
        ).Value;
        var upgradeOps = (
            (OperationsResultOk)
                SchemaDiff.Calculate(currentSchema, v2, allowDestructive: true, logger: _logger)
        ).Value;

        // Assert - Should have drop + create
        Assert.Equal(2, upgradeOps.Count);
        Assert.Contains(upgradeOps, op => op is DropIndexOperation);
        Assert.Contains(upgradeOps, op => op is CreateIndexOperation);

        var result = MigrationRunner.Apply(
            _connection,
            upgradeOps,
            PostgresDdlGenerator.Generate,
            MigrationOptions.Destructive,
            _logger
        );

        Assert.True(
            result is MigrationApplyResultOk,
            $"Upgrade failed: {(result as MigrationApplyResultError)?.Value}"
        );

        // Verify new column index exists (no lower() function)
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            SELECT indexdef FROM pg_indexes
            WHERE tablename = 'venues' AND indexname = 'idx_venues_name'
            """;
        var indexDef = (string?)cmd.ExecuteScalar();
        Assert.NotNull(indexDef);
        Assert.DoesNotContain("lower", indexDef);
    }

    [Fact]
    public void UpgradeIndex_SameNameDifferentType_NotDetectedWithoutDestructive()
    {
        // This test verifies that WITHOUT allowDestructive, the system doesn't
        // automatically detect that an index definition changed (column vs expression).
        // Converting an index type is a destructive operation requiring explicit opt-in.

        // Arrange - Create table with regular column index
        var v1 = Schema
            .Define("Test")
            .Table(
                "public",
                "products",
                t =>
                    t.Column("id", PortableTypes.Uuid, c => c.PrimaryKey())
                        .Column("name", PortableTypes.VarChar(200), c => c.NotNull())
                        .Index("idx_products_name", "name", unique: true)
            )
            .Build();

        var emptySchema = (
            (SchemaResultOk)PostgresSchemaInspector.Inspect(_connection, "public", _logger)
        ).Value;
        var v1Ops = (
            (OperationsResultOk)SchemaDiff.Calculate(emptySchema, v1, logger: _logger)
        ).Value;
        _ = MigrationRunner.Apply(
            _connection,
            v1Ops,
            PostgresDdlGenerator.Generate,
            MigrationOptions.Default,
            _logger
        );

        // v2 wants to change to expression index with SAME name
        // This is a semantic change - case-sensitive to case-insensitive
        var v2 = Schema
            .Define("Test")
            .Table(
                "public",
                "products",
                t =>
                    t.Column("id", PortableTypes.Uuid, c => c.PrimaryKey())
                        .Column("name", PortableTypes.VarChar(200), c => c.NotNull())
                        .ExpressionIndex("idx_products_name", "lower(name)", unique: true)
            )
            .Build();

        // Act - Calculate without destructive flag
        var currentSchema = (
            (SchemaResultOk)PostgresSchemaInspector.Inspect(_connection, "public", _logger)
        ).Value;
        var upgradeOps = (
            (OperationsResultOk)
                SchemaDiff.Calculate(currentSchema, v2, allowDestructive: false, logger: _logger)
        ).Value;

        // Assert - No operations since index name already exists and we can't drop without destructive
        // The SchemaDiff only compares by name, not by definition
        Assert.Empty(upgradeOps);
    }

    [Fact]
    public void Destructive_DropTable_AllowedWithOption()
    {
        // Arrange - Create initial tables
        var v1 = Schema
            .Define("Test")
            .Table(
                "public",
                "keepers",
                t => t.Column("id", PortableTypes.Uuid, c => c.PrimaryKey())
            )
            .Table("public", "dropme", t => t.Column("id", PortableTypes.Uuid, c => c.PrimaryKey()))
            .Build();

        var emptySchema = (
            (SchemaResultOk)PostgresSchemaInspector.Inspect(_connection, "public", _logger)
        ).Value;
        var v1Ops = (
            (OperationsResultOk)SchemaDiff.Calculate(emptySchema, v1, logger: _logger)
        ).Value;
        _ = MigrationRunner.Apply(
            _connection,
            v1Ops,
            PostgresDdlGenerator.Generate,
            MigrationOptions.Default,
            _logger
        );

        // v2 removes dropme table
        var v2 = Schema
            .Define("Test")
            .Table(
                "public",
                "keepers",
                t => t.Column("id", PortableTypes.Uuid, c => c.PrimaryKey())
            )
            .Build();

        // Act
        var currentSchema = (
            (SchemaResultOk)PostgresSchemaInspector.Inspect(_connection, "public", _logger)
        ).Value;

        var operations = (
            (OperationsResultOk)
                SchemaDiff.Calculate(currentSchema, v2, allowDestructive: true, logger: _logger)
        ).Value;

        Assert.Single(operations);
        Assert.IsType<DropTableOperation>(operations[0]);

        var result = MigrationRunner.Apply(
            _connection,
            operations,
            PostgresDdlGenerator.Generate,
            MigrationOptions.Destructive,
            _logger
        );

        // Assert
        Assert.True(result is MigrationApplyResultOk);

        var finalSchema = (
            (SchemaResultOk)PostgresSchemaInspector.Inspect(_connection, "public", _logger)
        ).Value;
        Assert.DoesNotContain(finalSchema.Tables, t => t.Name == "dropme");
        Assert.Contains(finalSchema.Tables, t => t.Name == "keepers");
    }

    // =============================================================================
    // LQL Default Value Tests - Platform Independent Defaults
    // =============================================================================

    [Fact]
    public void LqlDefault_NowFunction_GeneratesCurrentTimestamp()
    {
        // Arrange - Create table with LQL now() default
        var schema = Schema
            .Define("Test")
            .Table(
                "public",
                "events",
                t =>
                    t.Column("id", PortableTypes.Uuid, c => c.PrimaryKey())
                        .Column(
                            "created_at",
                            PortableTypes.DateTimeOffset,
                            c => c.NotNull().DefaultLql("now()")
                        )
            )
            .Build();

        // Act
        var emptySchema = (
            (SchemaResultOk)PostgresSchemaInspector.Inspect(_connection, "public", _logger)
        ).Value;
        var operations = (
            (OperationsResultOk)SchemaDiff.Calculate(emptySchema, schema, logger: _logger)
        ).Value;

        var result = MigrationRunner.Apply(
            _connection,
            operations,
            PostgresDdlGenerator.Generate,
            MigrationOptions.Default,
            _logger
        );

        Assert.True(result is MigrationApplyResultOk);

        // Insert a row without specifying created_at - should use default
        using var insertCmd = _connection.CreateCommand();
        insertCmd.CommandText =
            "INSERT INTO events (id) VALUES ('11111111-1111-1111-1111-111111111111')";
        insertCmd.ExecuteNonQuery();

        // Verify the default was applied - should be a recent timestamp
        using var selectCmd = _connection.CreateCommand();
        selectCmd.CommandText =
            "SELECT created_at FROM events WHERE id = '11111111-1111-1111-1111-111111111111'";
        var createdAt = (DateTime)selectCmd.ExecuteScalar()!;

        // Should be within last few seconds
        Assert.True((DateTime.UtcNow - createdAt).TotalSeconds < 10);
    }

    [Fact]
    public void LqlDefault_GenUuidFunction_GeneratesValidUuid()
    {
        // Arrange - Create table with LQL gen_uuid() default
        var schema = Schema
            .Define("Test")
            .Table(
                "public",
                "items",
                t =>
                    t.Column("id", PortableTypes.Uuid, c => c.PrimaryKey().DefaultLql("gen_uuid()"))
                        .Column("name", PortableTypes.VarChar(100), c => c.NotNull())
            )
            .Build();

        // Act
        var emptySchema = (
            (SchemaResultOk)PostgresSchemaInspector.Inspect(_connection, "public", _logger)
        ).Value;
        var operations = (
            (OperationsResultOk)SchemaDiff.Calculate(emptySchema, schema, logger: _logger)
        ).Value;

        var result = MigrationRunner.Apply(
            _connection,
            operations,
            PostgresDdlGenerator.Generate,
            MigrationOptions.Default,
            _logger
        );

        Assert.True(result is MigrationApplyResultOk);

        // Insert rows without specifying id - should generate unique UUIDs
        using var insertCmd = _connection.CreateCommand();
        insertCmd.CommandText =
            @"
            INSERT INTO items (name) VALUES ('Item 1');
            INSERT INTO items (name) VALUES ('Item 2');
            INSERT INTO items (name) VALUES ('Item 3');
        ";
        insertCmd.ExecuteNonQuery();

        // Verify UUIDs were generated and are unique
        using var selectCmd = _connection.CreateCommand();
        selectCmd.CommandText = "SELECT id FROM items ORDER BY name";
        using var reader = selectCmd.ExecuteReader();

        var uuids = new List<Guid>();
        while (reader.Read())
        {
            uuids.Add(reader.GetGuid(0));
        }

        Assert.Equal(3, uuids.Count);
        Assert.Equal(3, uuids.Distinct().Count()); // All unique
        Assert.All(uuids, id => Assert.NotEqual(Guid.Empty, id));
    }

    [Fact]
    public void LqlDefault_BooleanTrue_GeneratesCorrectValue()
    {
        // Arrange
        var schema = Schema
            .Define("Test")
            .Table(
                "public",
                "flags",
                t =>
                    t.Column("id", PortableTypes.Uuid, c => c.PrimaryKey())
                        .Column(
                            "is_active",
                            PortableTypes.Boolean,
                            c => c.NotNull().DefaultLql("true")
                        )
                        .Column(
                            "is_deleted",
                            PortableTypes.Boolean,
                            c => c.NotNull().DefaultLql("false")
                        )
            )
            .Build();

        // Act
        var emptySchema = (
            (SchemaResultOk)PostgresSchemaInspector.Inspect(_connection, "public", _logger)
        ).Value;
        var operations = (
            (OperationsResultOk)SchemaDiff.Calculate(emptySchema, schema, logger: _logger)
        ).Value;

        var result = MigrationRunner.Apply(
            _connection,
            operations,
            PostgresDdlGenerator.Generate,
            MigrationOptions.Default,
            _logger
        );

        Assert.True(result is MigrationApplyResultOk);

        // Insert without specifying booleans
        using var insertCmd = _connection.CreateCommand();
        insertCmd.CommandText =
            "INSERT INTO flags (id) VALUES ('11111111-1111-1111-1111-111111111111')";
        insertCmd.ExecuteNonQuery();

        // Verify defaults
        using var selectCmd = _connection.CreateCommand();
        selectCmd.CommandText = "SELECT is_active, is_deleted FROM flags";
        using var reader = selectCmd.ExecuteReader();
        Assert.True(reader.Read());
        Assert.True(reader.GetBoolean(0)); // is_active = true
        Assert.False(reader.GetBoolean(1)); // is_deleted = false
    }

    [Fact]
    public void LqlDefault_NumericLiterals_GeneratesCorrectValues()
    {
        // Arrange - Test integer and decimal defaults
        var schema = Schema
            .Define("Test")
            .Table(
                "public",
                "counters",
                t =>
                    t.Column("id", PortableTypes.Uuid, c => c.PrimaryKey())
                        .Column("count", PortableTypes.Int, c => c.NotNull().DefaultLql("0"))
                        .Column(
                            "score",
                            PortableTypes.Decimal(10, 2),
                            c => c.NotNull().DefaultLql("100")
                        )
                        .Column("rate", PortableTypes.Double, c => c.NotNull().DefaultLql("0.5"))
            )
            .Build();

        // Act
        var emptySchema = (
            (SchemaResultOk)PostgresSchemaInspector.Inspect(_connection, "public", _logger)
        ).Value;
        var operations = (
            (OperationsResultOk)SchemaDiff.Calculate(emptySchema, schema, logger: _logger)
        ).Value;

        var result = MigrationRunner.Apply(
            _connection,
            operations,
            PostgresDdlGenerator.Generate,
            MigrationOptions.Default,
            _logger
        );

        Assert.True(result is MigrationApplyResultOk);

        // Insert without specifying values
        using var insertCmd = _connection.CreateCommand();
        insertCmd.CommandText =
            "INSERT INTO counters (id) VALUES ('11111111-1111-1111-1111-111111111111')";
        insertCmd.ExecuteNonQuery();

        // Verify defaults
        using var selectCmd = _connection.CreateCommand();
        selectCmd.CommandText = "SELECT count, score, rate FROM counters";
        using var reader = selectCmd.ExecuteReader();
        Assert.True(reader.Read());
        Assert.Equal(0, reader.GetInt32(0));
        Assert.Equal(100m, reader.GetDecimal(1));
        Assert.Equal(0.5, reader.GetDouble(2), 2);
    }

    [Fact]
    public void LqlDefault_StringLiteral_GeneratesCorrectValue()
    {
        // Arrange - Test string literal default with single quotes
        var schema = Schema
            .Define("Test")
            .Table(
                "public",
                "statuses",
                t =>
                    t.Column("id", PortableTypes.Uuid, c => c.PrimaryKey())
                        .Column(
                            "status",
                            PortableTypes.VarChar(50),
                            c => c.NotNull().DefaultLql("'pending'")
                        )
                        .Column(
                            "category",
                            PortableTypes.VarChar(50),
                            c => c.NotNull().DefaultLql("'default'")
                        )
            )
            .Build();

        // Act
        var emptySchema = (
            (SchemaResultOk)PostgresSchemaInspector.Inspect(_connection, "public", _logger)
        ).Value;
        var operations = (
            (OperationsResultOk)SchemaDiff.Calculate(emptySchema, schema, logger: _logger)
        ).Value;

        var result = MigrationRunner.Apply(
            _connection,
            operations,
            PostgresDdlGenerator.Generate,
            MigrationOptions.Default,
            _logger
        );

        Assert.True(result is MigrationApplyResultOk);

        // Insert without specifying strings
        using var insertCmd = _connection.CreateCommand();
        insertCmd.CommandText =
            "INSERT INTO statuses (id) VALUES ('11111111-1111-1111-1111-111111111111')";
        insertCmd.ExecuteNonQuery();

        // Verify defaults
        using var selectCmd = _connection.CreateCommand();
        selectCmd.CommandText = "SELECT status, category FROM statuses";
        using var reader = selectCmd.ExecuteReader();
        Assert.True(reader.Read());
        Assert.Equal("pending", reader.GetString(0));
        Assert.Equal("default", reader.GetString(1));
    }

    [Fact]
    public void LqlDefault_AllTypesInOneTable_WorksTogether()
    {
        // Arrange - Comprehensive test with all LQL default types
        var schema = Schema
            .Define("Test")
            .Table(
                "public",
                "comprehensive_defaults",
                t =>
                    t.Column("id", PortableTypes.Uuid, c => c.PrimaryKey().DefaultLql("gen_uuid()"))
                        .Column("name", PortableTypes.VarChar(100), c => c.NotNull())
                        .Column(
                            "created_at",
                            PortableTypes.DateTimeOffset,
                            c => c.NotNull().DefaultLql("now()")
                        )
                        .Column(
                            "is_active",
                            PortableTypes.Boolean,
                            c => c.NotNull().DefaultLql("true")
                        )
                        .Column(
                            "is_archived",
                            PortableTypes.Boolean,
                            c => c.NotNull().DefaultLql("false")
                        )
                        .Column("count", PortableTypes.Int, c => c.NotNull().DefaultLql("0"))
                        .Column("priority", PortableTypes.Int, c => c.NotNull().DefaultLql("5"))
                        .Column(
                            "status",
                            PortableTypes.VarChar(20),
                            c => c.NotNull().DefaultLql("'active'")
                        )
            )
            .Build();

        // Act
        var emptySchema = (
            (SchemaResultOk)PostgresSchemaInspector.Inspect(_connection, "public", _logger)
        ).Value;
        var operations = (
            (OperationsResultOk)SchemaDiff.Calculate(emptySchema, schema, logger: _logger)
        ).Value;

        var result = MigrationRunner.Apply(
            _connection,
            operations,
            PostgresDdlGenerator.Generate,
            MigrationOptions.Default,
            _logger
        );

        Assert.True(result is MigrationApplyResultOk);

        // Insert only name - all other columns should use defaults
        using var insertCmd = _connection.CreateCommand();
        insertCmd.CommandText = "INSERT INTO comprehensive_defaults (name) VALUES ('Test Item')";
        insertCmd.ExecuteNonQuery();

        // Verify all defaults
        using var selectCmd = _connection.CreateCommand();
        selectCmd.CommandText =
            "SELECT id, created_at, is_active, is_archived, count, priority, status FROM comprehensive_defaults";
        using var reader = selectCmd.ExecuteReader();
        Assert.True(reader.Read());

        var id = reader.GetGuid(0);
        Assert.NotEqual(Guid.Empty, id);

        var createdAt = reader.GetDateTime(1);
        Assert.True((DateTime.UtcNow - createdAt).TotalSeconds < 10);

        Assert.True(reader.GetBoolean(2)); // is_active
        Assert.False(reader.GetBoolean(3)); // is_archived
        Assert.Equal(0, reader.GetInt32(4)); // count
        Assert.Equal(5, reader.GetInt32(5)); // priority
        Assert.Equal("active", reader.GetString(6)); // status
    }

    // ═══════════════════════════════════════════════════════════════════
    // VECTOR COLUMN E2E TESTS — pgvector extension
    //
    // Requires the shared fixture to use pgvector/pgvector:pg16 (a
    // drop-in superset of postgres:16 with the vector extension
    // preinstalled). These tests prove the full pipeline — YAML schema
    // -> VectorType -> PostgresDdlGenerator -> CREATE EXTENSION vector
    // -> CREATE TABLE -> INSERT -> SELECT -> round-trip — works against
    // a real database, which is the exact path HealthcareSamples needs
    // to unblock its embeddings column.
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void CreateTableWithVectorColumn_MigratesAgainstPgvectorContainer_Success()
    {
        // Arrange — a Documents table with a 384-dim embedding, the
        // sentence-transformers default.
        var schema = Schema
            .Define("Embeddings")
            .Table(
                "public",
                "documents",
                t =>
                    t.Column("id", PortableTypes.Uuid, c => c.PrimaryKey())
                        .Column("body", PortableTypes.Text, c => c.NotNull())
                        .Column("embedding", PortableTypes.Vector(384), c => c.NotNull())
            )
            .Build();

        // The pgvector extension must exist in the database before any
        // vector(N) column can be created. The migration pipeline is
        // expected to emit `CREATE EXTENSION IF NOT EXISTS vector` as
        // part of applying a schema that contains a VectorType column.
        // If the source-side fix is still in progress, this test will
        // fail loudly on CREATE TABLE, proving the gap.

        var emptySchema = (
            (SchemaResultOk)PostgresSchemaInspector.Inspect(_connection, "public", _logger)
        ).Value;

        var operations = (
            (OperationsResultOk)SchemaDiff.Calculate(emptySchema, schema, logger: _logger)
        ).Value;

        // Act
        var result = MigrationRunner.Apply(
            _connection,
            operations,
            PostgresDdlGenerator.Generate,
            MigrationOptions.Default,
            _logger
        );

        // Assert — migration succeeded
        Assert.True(
            result is MigrationApplyResultOk,
            $"Vector migration failed: {(result as MigrationApplyResultError)?.Value}"
        );

        // Assert — documents table exists
        var inspected = (
            (SchemaResultOk)PostgresSchemaInspector.Inspect(_connection, "public", _logger)
        ).Value;
        Assert.Contains(inspected.Tables, t => t.Name == "documents");

        // Assert — pgvector extension is installed in the database
        using (var extCmd = _connection.CreateCommand())
        {
            extCmd.CommandText = "SELECT COUNT(*) FROM pg_extension WHERE extname = 'vector'";
            var extCount = Convert.ToInt32(
                extCmd.ExecuteScalar(),
                System.Globalization.CultureInfo.InvariantCulture
            );
            Assert.Equal(1, extCount);
        }

        // Assert — embedding column has the real pgvector type with the
        // correct dimension, not TEXT / BYTEA / anything else.
        using (var typeCmd = _connection.CreateCommand())
        {
            typeCmd.CommandText =
                "SELECT format_type(a.atttypid, a.atttypmod) "
                + "FROM pg_attribute a "
                + "JOIN pg_class c ON c.oid = a.attrelid "
                + "JOIN pg_namespace n ON n.oid = c.relnamespace "
                + "WHERE n.nspname = 'public' "
                + "  AND c.relname = 'documents' "
                + "  AND a.attname = 'embedding'";
            var colType = (string?)typeCmd.ExecuteScalar();
            Assert.Equal("vector(384)", colType);
        }

        // Act — round-trip insert + select a known vector
        using (var insertCmd = _connection.CreateCommand())
        {
            var vecLiteral =
                "[" + string.Join(",", Enumerable.Range(0, 384).Select(i => "0.5")) + "]";
            insertCmd.CommandText =
                "INSERT INTO public.documents (id, body, embedding) "
                + "VALUES (gen_random_uuid(), 'hello', '"
                + vecLiteral
                + "'::vector)";
            var rows = insertCmd.ExecuteNonQuery();
            Assert.Equal(1, rows);
        }

        using (var selectCmd = _connection.CreateCommand())
        {
            selectCmd.CommandText = "SELECT body, vector_dims(embedding) FROM public.documents";
            using var reader = selectCmd.ExecuteReader();
            Assert.True(reader.Read());
            Assert.Equal("hello", reader.GetString(0));
            Assert.Equal(384, reader.GetInt32(1));
        }
    }

    [Fact]
    public void CreateTableWithVectorColumn_OpenAiLargeDim_Success()
    {
        // Arrange — the 3072-dim OpenAI text-embedding-3-large dimension,
        // larger than the pgvector default ivfflat index limit, exercises
        // the high end of the dimension range.
        var schema = Schema
            .Define("EmbeddingsLarge")
            .Table(
                "public",
                "large_embeddings",
                t =>
                    t.Column("id", PortableTypes.Uuid, c => c.PrimaryKey())
                        .Column("embedding", PortableTypes.Vector(3072), c => c.NotNull())
            )
            .Build();

        var emptySchema = (
            (SchemaResultOk)PostgresSchemaInspector.Inspect(_connection, "public", _logger)
        ).Value;
        var operations = (
            (OperationsResultOk)SchemaDiff.Calculate(emptySchema, schema, logger: _logger)
        ).Value;

        // Act
        var result = MigrationRunner.Apply(
            _connection,
            operations,
            PostgresDdlGenerator.Generate,
            MigrationOptions.Default,
            _logger
        );

        // Assert
        Assert.True(
            result is MigrationApplyResultOk,
            $"Large-dim vector migration failed: {(result as MigrationApplyResultError)?.Value}"
        );

        using var typeCmd = _connection.CreateCommand();
        typeCmd.CommandText =
            "SELECT format_type(a.atttypid, a.atttypmod) "
            + "FROM pg_attribute a "
            + "JOIN pg_class c ON c.oid = a.attrelid "
            + "WHERE c.relname = 'large_embeddings' AND a.attname = 'embedding'";
        Assert.Equal("vector(3072)", (string?)typeCmd.ExecuteScalar());
    }

    private void InsertPatientGender(string gender)
    {
        using var command = _connection.CreateCommand();
        command.CommandText =
            "INSERT INTO \"fhir_patient\" (\"id\", \"Gender\") VALUES (@id, @gender)";
        command.Parameters.AddWithValue("@id", Guid.NewGuid());
        command.Parameters.AddWithValue("@gender", gender);
        command.ExecuteNonQuery();
    }
}
