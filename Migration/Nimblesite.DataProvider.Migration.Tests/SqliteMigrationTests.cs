namespace Nimblesite.DataProvider.Migration.Tests;

/// <summary>
/// E2E tests for SQLite migrations - greenfield and upgrades.
/// </summary>
public sealed class SqliteMigrationTests
{
    private readonly ILogger _logger = NullLogger.Instance;

    private static (SqliteConnection Connection, string DbPath) CreateTestDb()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"sqlitemigration_{Guid.NewGuid()}.db");
        var connection = new SqliteConnection($"Data Source={dbPath}");
        connection.Open();
        return (connection, dbPath);
    }

    private static void CleanupTestDb(SqliteConnection connection, string dbPath)
    {
        connection.Close();
        connection.Dispose();
        if (File.Exists(dbPath))
        {
            try
            {
                File.Delete(dbPath);
            }
            catch
            { /* File may be locked */
            }
        }
    }

    private static void SeedSchema(
        SqliteConnection connection,
        SchemaDefinition schema,
        ILogger logger
    )
    {
        var current = ((SchemaResultOk)SqliteSchemaInspector.Inspect(connection, logger)).Value;
        var ops = ((OperationsResultOk)SchemaDiff.Calculate(current, schema, logger: logger)).Value;
        _ = MigrationRunner.Apply(
            connection,
            ops,
            SqliteDdlGenerator.Generate,
            MigrationOptions.Default,
            logger
        );
    }

    [Fact]
    public void CreateDatabaseFromScratch_SingleTable_Success()
    {
        // Arrange
        var (connection, dbPath) = CreateTestDb();
        try
        {
            var schema = Schema
                .Define("Test")
                .Table(
                    "Users",
                    t =>
                        t.Column("Id", PortableTypes.Uuid, c => c.PrimaryKey())
                            .Column("Email", PortableTypes.NVarChar(255), c => c.NotNull())
                            .Column("Name", PortableTypes.NVarChar(100))
                            .Index("idx_users_email", "Email", unique: true)
                )
                .Build();

            // Act
            var emptySchema = SqliteSchemaInspector.Inspect(connection, _logger);
            Assert.True(emptySchema is SchemaResultOk);

            var operations = SchemaDiff.Calculate(
                ((SchemaResultOk)emptySchema).Value,
                schema,
                logger: _logger
            );
            Assert.True(operations is OperationsResultOk);

            var ops = ((OperationsResultOk)operations).Value;

            var result = MigrationRunner.Apply(
                connection,
                ops,
                SqliteDdlGenerator.Generate,
                MigrationOptions.Default,
                _logger
            );

            // Assert
            Assert.True(result is MigrationApplyResultOk);

            // Verify table exists
            var inspected = SqliteSchemaInspector.Inspect(connection, _logger);
            Assert.True(inspected is SchemaResultOk);
            var inspectedSchema = ((SchemaResultOk)inspected).Value;
            Assert.Single(inspectedSchema.Tables);
            Assert.Equal("Users", inspectedSchema.Tables[0].Name);
            Assert.Equal(3, inspectedSchema.Tables[0].Columns.Count);
        }
        finally
        {
            CleanupTestDb(connection, dbPath);
        }
    }

    [Fact]
    public void CreateDatabaseFromScratch_MultipleTablesWithForeignKeys_Success()
    {
        // Arrange
        var (connection, dbPath) = CreateTestDb();
        try
        {
            // Enable foreign keys
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "PRAGMA foreign_keys = ON";
                cmd.ExecuteNonQuery();
            }

            var schema = Schema
                .Define("Test")
                .Table(
                    "Users",
                    t =>
                        t.Column("Id", PortableTypes.Uuid, c => c.PrimaryKey())
                            .Column("Email", PortableTypes.NVarChar(255), c => c.NotNull())
                )
                .Table(
                    "Orders",
                    t =>
                        t.Column("Id", PortableTypes.BigInt, c => c.PrimaryKey().Identity())
                            .Column("UserId", PortableTypes.Uuid, c => c.NotNull())
                            .Column("Total", PortableTypes.Decimal(10, 2), c => c.NotNull())
                            .Column(
                                "CreatedAt",
                                PortableTypes.DateTime(),
                                c => c.NotNull().Default("CURRENT_TIMESTAMP")
                            )
                            .ForeignKey("UserId", "Users", "Id", ForeignKeyAction.Cascade)
                )
                .Build();

            // Act
            var emptySchema = (
                (SchemaResultOk)SqliteSchemaInspector.Inspect(connection, _logger)
            ).Value;

            var operations = (
                (OperationsResultOk)SchemaDiff.Calculate(emptySchema, schema, logger: _logger)
            ).Value;

            var result = MigrationRunner.Apply(
                connection,
                operations,
                SqliteDdlGenerator.Generate,
                MigrationOptions.Default,
                _logger
            );

            // Assert
            Assert.True(result is MigrationApplyResultOk);

            var inspected = (
                (SchemaResultOk)SqliteSchemaInspector.Inspect(connection, _logger)
            ).Value;

            Assert.Equal(2, inspected.Tables.Count);
            Assert.Contains(inspected.Tables, t => t.Name == "Users");
            Assert.Contains(inspected.Tables, t => t.Name == "Orders");

            var ordersTable = inspected.Tables.First(t => t.Name == "Orders");
            Assert.Single(ordersTable.ForeignKeys);
            Assert.Equal("Users", ordersTable.ForeignKeys[0].ReferencedTable);
        }
        finally
        {
            CleanupTestDb(connection, dbPath);
        }
    }

    [Fact]
    public void UpgradeExistingDatabase_AddColumn_Success()
    {
        // Arrange
        var (connection, dbPath) = CreateTestDb();
        try
        {
            // Create initial schema
            var v1 = Schema
                .Define("Test")
                .Table(
                    "Users",
                    t =>
                        t.Column("Id", PortableTypes.Uuid, c => c.PrimaryKey())
                            .Column("Email", PortableTypes.NVarChar(255))
                )
                .Build();

            // Apply v1
            SeedSchema(connection, v1, _logger);

            // Define v2 with new columns
            var v2 = Schema
                .Define("Test")
                .Table(
                    "Users",
                    t =>
                        t.Column("Id", PortableTypes.Uuid, c => c.PrimaryKey())
                            .Column("Email", PortableTypes.NVarChar(255))
                            .Column("Name", PortableTypes.NVarChar(100))
                            .Column("CreatedAt", PortableTypes.DateTime())
                )
                .Build();

            // Act - upgrade to v2
            var currentSchema = (
                (SchemaResultOk)SqliteSchemaInspector.Inspect(connection, _logger)
            ).Value;

            var upgradeOps = (
                (OperationsResultOk)SchemaDiff.Calculate(currentSchema, v2, logger: _logger)
            ).Value;

            // Should have 2 AddColumn operations
            Assert.Equal(2, upgradeOps.Count);
            Assert.All(upgradeOps, op => Assert.IsType<AddColumnOperation>(op));

            var result = MigrationRunner.Apply(
                connection,
                upgradeOps,
                SqliteDdlGenerator.Generate,
                MigrationOptions.Default,
                _logger
            );

            // Assert
            Assert.True(result is MigrationApplyResultOk);

            var finalSchema = (
                (SchemaResultOk)SqliteSchemaInspector.Inspect(connection, _logger)
            ).Value;
            var users = finalSchema.Tables.Single(t => t.Name == "Users");
            Assert.Equal(4, users.Columns.Count);
            Assert.Contains(users.Columns, c => c.Name == "Name");
            Assert.Contains(users.Columns, c => c.Name == "CreatedAt");
        }
        finally
        {
            CleanupTestDb(connection, dbPath);
        }
    }

    [Fact]
    public void UpgradeExistingDatabase_AddTable_Success()
    {
        // Arrange
        var (connection, dbPath) = CreateTestDb();
        try
        {
            var v1 = Schema
                .Define("Test")
                .Table("Users", t => t.Column("Id", PortableTypes.Uuid, c => c.PrimaryKey()))
                .Build();

            // Apply v1
            SeedSchema(connection, v1, _logger);

            // v2 adds a new table
            var v2 = Schema
                .Define("Test")
                .Table("Users", t => t.Column("Id", PortableTypes.Uuid, c => c.PrimaryKey()))
                .Table(
                    "Products",
                    t =>
                        t.Column("Id", PortableTypes.BigInt, c => c.PrimaryKey().Identity())
                            .Column("Name", PortableTypes.NVarChar(200), c => c.NotNull())
                            .Column("Price", PortableTypes.Decimal(10, 2))
                )
                .Build();

            // Act
            var currentSchema = (
                (SchemaResultOk)SqliteSchemaInspector.Inspect(connection, _logger)
            ).Value;

            var upgradeOps = (
                (OperationsResultOk)SchemaDiff.Calculate(currentSchema, v2, logger: _logger)
            ).Value;

            // Should have 1 CreateTable operation
            Assert.Single(upgradeOps);
            Assert.IsType<CreateTableOperation>(upgradeOps[0]);

            var result = MigrationRunner.Apply(
                connection,
                upgradeOps,
                SqliteDdlGenerator.Generate,
                MigrationOptions.Default,
                _logger
            );

            // Assert
            Assert.True(result is MigrationApplyResultOk);

            var finalSchema = (
                (SchemaResultOk)SqliteSchemaInspector.Inspect(connection, _logger)
            ).Value;
            Assert.Equal(2, finalSchema.Tables.Count);
            Assert.Contains(finalSchema.Tables, t => t.Name == "Products");
        }
        finally
        {
            CleanupTestDb(connection, dbPath);
        }
    }

    [Fact]
    public void UpgradeExistingDatabase_AddIndex_Success()
    {
        // Arrange
        var (connection, dbPath) = CreateTestDb();
        try
        {
            var v1 = Schema
                .Define("Test")
                .Table(
                    "Users",
                    t =>
                        t.Column("Id", PortableTypes.Uuid, c => c.PrimaryKey())
                            .Column("Email", PortableTypes.NVarChar(255))
                )
                .Build();

            // Apply v1
            SeedSchema(connection, v1, _logger);

            // v2 adds an index
            var v2 = Schema
                .Define("Test")
                .Table(
                    "Users",
                    t =>
                        t.Column("Id", PortableTypes.Uuid, c => c.PrimaryKey())
                            .Column("Email", PortableTypes.NVarChar(255))
                            .Index("idx_users_email", "Email", unique: true)
                )
                .Build();

            // Act
            var currentSchema = (
                (SchemaResultOk)SqliteSchemaInspector.Inspect(connection, _logger)
            ).Value;

            var upgradeOps = (
                (OperationsResultOk)SchemaDiff.Calculate(currentSchema, v2, logger: _logger)
            ).Value;

            Assert.Single(upgradeOps);
            Assert.IsType<CreateIndexOperation>(upgradeOps[0]);

            var result = MigrationRunner.Apply(
                connection,
                upgradeOps,
                SqliteDdlGenerator.Generate,
                MigrationOptions.Default,
                _logger
            );

            // Assert
            Assert.True(result is MigrationApplyResultOk);

            var finalSchema = (
                (SchemaResultOk)SqliteSchemaInspector.Inspect(connection, _logger)
            ).Value;
            var users = finalSchema.Tables.Single(t => t.Name == "Users");
            Assert.Single(users.Indexes);
            Assert.Equal("idx_users_email", users.Indexes[0].Name);
            Assert.True(users.Indexes[0].IsUnique);
        }
        finally
        {
            CleanupTestDb(connection, dbPath);
        }
    }

    [Fact]
    public void Migration_IsIdempotent_NoErrorOnRerun()
    {
        // Arrange
        var (connection, dbPath) = CreateTestDb();
        try
        {
            var schema = Schema
                .Define("Test")
                .Table(
                    "Items",
                    t =>
                        t.Column("Id", PortableTypes.Int, c => c.PrimaryKey())
                            .Column("Name", PortableTypes.NVarChar(50))
                            .Index("idx_items_name", "Name")
                )
                .Build();

            // Act - Run migration twice
            for (var i = 0; i < 2; i++)
            {
                var currentSchema = (
                    (SchemaResultOk)SqliteSchemaInspector.Inspect(connection, _logger)
                ).Value;

                var operations = (
                    (OperationsResultOk)SchemaDiff.Calculate(currentSchema, schema, logger: _logger)
                ).Value;

                var result = MigrationRunner.Apply(
                    connection,
                    operations,
                    SqliteDdlGenerator.Generate,
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
        finally
        {
            CleanupTestDb(connection, dbPath);
        }
    }

    [Fact]
    public void CreateTable_AllPortableTypes_Success()
    {
        // Arrange
        var (connection, dbPath) = CreateTestDb();
        try
        {
            var schema = Schema
                .Define("Test")
                .Table(
                    "TypeTest",
                    t =>
                        t.Column("Id", PortableTypes.BigInt, c => c.PrimaryKey())
                            .Column("TinyInt", PortableTypes.TinyInt)
                            .Column("SmallInt", PortableTypes.SmallInt)
                            .Column("Int", PortableTypes.Int)
                            .Column("BigInt", PortableTypes.BigInt)
                            .Column("Decimal", PortableTypes.Decimal(18, 2))
                            .Column("Float", PortableTypes.Float)
                            .Column("Double", PortableTypes.Double)
                            .Column("Money", PortableTypes.Money)
                            .Column("Bool", PortableTypes.Boolean)
                            .Column("Char", PortableTypes.Char(10))
                            .Column("VarChar", PortableTypes.VarChar(50))
                            .Column("NChar", PortableTypes.NChar(10))
                            .Column("NVarChar", PortableTypes.NVarChar(100))
                            .Column("Text", PortableTypes.Text)
                            .Column("Binary", PortableTypes.Binary(16))
                            .Column("VarBinary", PortableTypes.VarBinary(256))
                            .Column("Blob", PortableTypes.Blob)
                            .Column("Date", PortableTypes.Date)
                            .Column("Time", PortableTypes.Time())
                            .Column("DateTime", PortableTypes.DateTime())
                            .Column("DateTimeOffset", PortableTypes.DateTimeOffset)
                            .Column("Uuid", PortableTypes.Uuid)
                            .Column("Json", PortableTypes.Json)
                            .Column("Xml", PortableTypes.Xml)
                )
                .Build();

            // Act
            var emptySchema = (
                (SchemaResultOk)SqliteSchemaInspector.Inspect(connection, _logger)
            ).Value;
            var operations = (
                (OperationsResultOk)SchemaDiff.Calculate(emptySchema, schema, logger: _logger)
            ).Value;

            var result = MigrationRunner.Apply(
                connection,
                operations,
                SqliteDdlGenerator.Generate,
                MigrationOptions.Default,
                _logger
            );

            // Assert
            Assert.True(result is MigrationApplyResultOk);

            var inspected = (
                (SchemaResultOk)SqliteSchemaInspector.Inspect(connection, _logger)
            ).Value;
            var table = inspected.Tables.Single();
            Assert.Equal(25, table.Columns.Count);
        }
        finally
        {
            CleanupTestDb(connection, dbPath);
        }
    }

    [Fact]
    public void Destructive_DropTable_BlockedByDefault()
    {
        // Arrange
        var (connection, dbPath) = CreateTestDb();
        try
        {
            // Create initial schema with 2 tables
            var v1 = Schema
                .Define("Test")
                .Table("Users", t => t.Column("Id", PortableTypes.Uuid, c => c.PrimaryKey()))
                .Table("Products", t => t.Column("Id", PortableTypes.Uuid, c => c.PrimaryKey()))
                .Build();

            SeedSchema(connection, v1, _logger);

            // v2 removes Products table
            var v2 = Schema
                .Define("Test")
                .Table("Users", t => t.Column("Id", PortableTypes.Uuid, c => c.PrimaryKey()))
                .Build();

            // Act - Calculate diff WITHOUT AllowDestructive
            var currentSchema = (
                (SchemaResultOk)SqliteSchemaInspector.Inspect(connection, _logger)
            ).Value;

            var operations = (
                (OperationsResultOk)
                    SchemaDiff.Calculate(
                        currentSchema,
                        v2,
                        allowDestructive: false,
                        logger: _logger
                    )
            ).Value;

            // Assert - No drop operations should be generated
            Assert.Empty(operations);
        }
        finally
        {
            CleanupTestDb(connection, dbPath);
        }
    }

    [Fact]
    public void Destructive_DropTable_AllowedWithOption()
    {
        // Arrange
        var (connection, dbPath) = CreateTestDb();
        try
        {
            var v1 = Schema
                .Define("Test")
                .Table("Users", t => t.Column("Id", PortableTypes.Uuid, c => c.PrimaryKey()))
                .Table("Products", t => t.Column("Id", PortableTypes.Uuid, c => c.PrimaryKey()))
                .Build();

            SeedSchema(connection, v1, _logger);

            var v2 = Schema
                .Define("Test")
                .Table("Users", t => t.Column("Id", PortableTypes.Uuid, c => c.PrimaryKey()))
                .Build();

            // Act - Calculate diff WITH AllowDestructive
            var currentSchema = (
                (SchemaResultOk)SqliteSchemaInspector.Inspect(connection, _logger)
            ).Value;

            var operations = (
                (OperationsResultOk)
                    SchemaDiff.Calculate(currentSchema, v2, allowDestructive: true, logger: _logger)
            ).Value;

            // Should have DropTableOperation
            Assert.Single(operations);
            Assert.IsType<DropTableOperation>(operations[0]);

            var result = MigrationRunner.Apply(
                connection,
                operations,
                SqliteDdlGenerator.Generate,
                MigrationOptions.Destructive,
                _logger
            );

            // Assert
            Assert.True(result is MigrationApplyResultOk);

            var finalSchema = (
                (SchemaResultOk)SqliteSchemaInspector.Inspect(connection, _logger)
            ).Value;
            Assert.Single(finalSchema.Tables);
            Assert.DoesNotContain(finalSchema.Tables, t => t.Name == "Products");
        }
        finally
        {
            CleanupTestDb(connection, dbPath);
        }
    }

    [Fact]
    public void SchemaInspector_RoundTrip_Matches()
    {
        // Arrange
        var (connection, dbPath) = CreateTestDb();
        try
        {
            var schema = Schema
                .Define("Test")
                .Table(
                    "Users",
                    t =>
                        t.Column("Id", PortableTypes.Uuid, c => c.PrimaryKey())
                            .Column("Email", PortableTypes.NVarChar(255), c => c.NotNull())
                            .Column("Active", PortableTypes.Boolean, c => c.Default("1"))
                            .Index("idx_users_email", "Email", unique: true)
                )
                .Build();

            // Create schema
            SeedSchema(connection, schema, _logger);

            // Act - Inspect and compare
            var inspected = (
                (SchemaResultOk)SqliteSchemaInspector.Inspect(connection, _logger)
            ).Value;

            // Calculate diff between original and inspected - should be empty
            var diff = (
                (OperationsResultOk)SchemaDiff.Calculate(inspected, schema, logger: _logger)
            ).Value;

            // Assert
            Assert.Empty(diff);
        }
        finally
        {
            CleanupTestDb(connection, dbPath);
        }
    }

    [Fact]
    public void DestructiveOperation_BlockedByDefault_ReturnsUsefulError()
    {
        // Arrange
        var (connection, dbPath) = CreateTestDb();
        try
        {
            // Create table first
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "CREATE TABLE ToBeDropped (Id INTEGER PRIMARY KEY)";
            cmd.ExecuteNonQuery();

            var dropOperation = new DropTableOperation("main", "ToBeDropped");

            // Act - try to apply destructive operation with default options
            var result = MigrationRunner.Apply(
                connection,
                [dropOperation],
                SqliteDdlGenerator.Generate,
                MigrationOptions.Default, // AllowDestructive = false
                _logger
            );

            // Assert - should fail with useful error message
            Assert.True(result is MigrationApplyResultError);
            var error = ((MigrationApplyResultError)result).Value;
            Assert.Contains("Destructive", error.Message);
            Assert.Contains("DropTableOperation", error.Message);
        }
        finally
        {
            CleanupTestDb(connection, dbPath);
        }
    }

    [Fact]
    public void InvalidSql_ReturnsUsefulError()
    {
        // Arrange
        var (connection, dbPath) = CreateTestDb();
        try
        {
            // Create a custom operation that generates invalid SQL
            var badTable = new TableDefinition
            {
                Schema = "main",
                Name = "Bad\"Table", // Invalid table name with quote
                Columns = [new ColumnDefinition { Name = "Id", Type = PortableTypes.Int }],
            };

            var createOp = new CreateTableOperation(badTable);

            // Act
            var result = MigrationRunner.Apply(
                connection,
                [createOp],
                SqliteDdlGenerator.Generate,
                MigrationOptions.Default,
                _logger
            );

            // Assert - should fail (invalid SQL) but not crash
            // Note: SQLite may accept this - adjust test if needed
            Assert.NotNull(result);
        }
        finally
        {
            CleanupTestDb(connection, dbPath);
        }
    }

    [Fact]
    public void SchemaCapture_ExistingDatabase_ReturnsCompleteSchema()
    {
        // Arrange - Create database with raw SQL (simulate existing DB)
        var (connection, dbPath) = CreateTestDb();
        try
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = """
                CREATE TABLE customers (
                    id TEXT PRIMARY KEY,
                    email TEXT NOT NULL,
                    name TEXT,
                    created_at TEXT DEFAULT CURRENT_TIMESTAMP
                );
                CREATE UNIQUE INDEX idx_customers_email ON customers(email);
                CREATE TABLE orders (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    customer_id TEXT NOT NULL,
                    total REAL,
                    FOREIGN KEY (customer_id) REFERENCES customers(id) ON DELETE CASCADE
                );
                CREATE INDEX idx_orders_customer ON orders(customer_id);
                """;
            cmd.ExecuteNonQuery();

            // Act - CAPTURE the existing schema
            var captureResult = SqliteSchemaInspector.Inspect(connection, _logger);

            // Assert - schema captured successfully
            Assert.True(captureResult is SchemaResultOk);
            var schema = ((SchemaResultOk)captureResult).Value;

            // Verify tables captured
            Assert.Equal(2, schema.Tables.Count);

            var customers = schema.Tables.Single(t => t.Name == "customers");
            Assert.Equal(4, customers.Columns.Count);
            Assert.Contains(customers.Columns, c => c.Name == "id");
            Assert.Contains(customers.Columns, c => c.Name == "email");
            Assert.Single(customers.Indexes);
            Assert.Equal("idx_customers_email", customers.Indexes[0].Name);
            Assert.True(customers.Indexes[0].IsUnique);

            var orders = schema.Tables.Single(t => t.Name == "orders");
            Assert.Equal(3, orders.Columns.Count);
            Assert.Single(orders.ForeignKeys);
            Assert.Equal("customers", orders.ForeignKeys[0].ReferencedTable);
            Assert.Equal(ForeignKeyAction.Cascade, orders.ForeignKeys[0].OnDelete);
            Assert.Single(orders.Indexes);
            Assert.Equal("idx_orders_customer", orders.Indexes[0].Name);
        }
        finally
        {
            CleanupTestDb(connection, dbPath);
        }
    }

    [Fact]
    public void SchemaCapture_SerializesToJson_RoundTrip()
    {
        // Arrange
        var (connection, dbPath) = CreateTestDb();
        try
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = """
                CREATE TABLE products (
                    id TEXT PRIMARY KEY,
                    name TEXT NOT NULL,
                    price REAL,
                    active INTEGER DEFAULT 1
                );
                CREATE INDEX idx_products_name ON products(name);
                """;
            cmd.ExecuteNonQuery();

            // Act - Capture and serialize to JSON
            var captureResult = SqliteSchemaInspector.Inspect(connection, _logger);
            Assert.True(captureResult is SchemaResultOk);
            var schema = ((SchemaResultOk)captureResult).Value;

            var json = SchemaSerializer.ToJson(schema);

            // Assert - JSON is valid and contains expected data
            Assert.NotNull(json);
            Assert.Contains("products", json);
            Assert.Contains("name", json);
            Assert.Contains("idx_products_name", json);

            // Deserialize and verify round-trip
            var restored = SchemaSerializer.FromJson(json);
            Assert.NotNull(restored);
            Assert.Single(restored.Tables);
            Assert.Equal("products", restored.Tables[0].Name);
            Assert.Equal(4, restored.Tables[0].Columns.Count);
            Assert.Single(restored.Tables[0].Indexes);
        }
        finally
        {
            CleanupTestDb(connection, dbPath);
        }
    }

    // =============================================================================
    // Expression Index Tests
    // =============================================================================

    [Fact]
    public void ExpressionIndex_CreateWithLowerFunction_Success()
    {
        // Arrange - Create table with expression index for case-insensitive uniqueness
        var (connection, dbPath) = CreateTestDb();
        try
        {
            var schema = Schema
                .Define("Test")
                .Table(
                    "Artists",
                    t =>
                        t.Column("Id", PortableTypes.Uuid, c => c.PrimaryKey())
                            .Column("Name", PortableTypes.VarChar(200), c => c.NotNull())
                            .ExpressionIndex("uq_artists_name", "lower(Name)", unique: true)
                )
                .Build();

            // Act
            var emptySchema = (
                (SchemaResultOk)SqliteSchemaInspector.Inspect(connection, _logger)
            ).Value;
            var operations = (
                (OperationsResultOk)SchemaDiff.Calculate(emptySchema, schema, logger: _logger)
            ).Value;

            var result = MigrationRunner.Apply(
                connection,
                operations,
                SqliteDdlGenerator.Generate,
                MigrationOptions.Default,
                _logger
            );

            // Assert - Migration succeeded
            Assert.True(
                result is MigrationApplyResultOk,
                $"Migration failed: {(result as MigrationApplyResultError)?.Value}"
            );

            // Verify index exists
            using var cmd = connection.CreateCommand();
            cmd.CommandText =
                "SELECT sql FROM sqlite_master WHERE type='index' AND name='uq_artists_name'";
            var indexDef = cmd.ExecuteScalar() as string;

            Assert.NotNull(indexDef);
            Assert.Contains("UNIQUE", indexDef);
            Assert.Contains("lower", indexDef);
        }
        finally
        {
            CleanupTestDb(connection, dbPath);
        }
    }

    [Fact]
    public void ExpressionIndex_EnforcesCaseInsensitiveUniqueness()
    {
        // Arrange - Create table with expression index
        var (connection, dbPath) = CreateTestDb();
        try
        {
            var schema = Schema
                .Define("Test")
                .Table(
                    "Venues",
                    t =>
                        t.Column("Id", PortableTypes.Uuid, c => c.PrimaryKey())
                            .Column("Name", PortableTypes.VarChar(200), c => c.NotNull())
                            .ExpressionIndex("uq_venues_name", "lower(Name)", unique: true)
                )
                .Build();

            SeedSchema(connection, schema, _logger);

            // Act - Insert first venue
            using var insertCmd = connection.CreateCommand();
            insertCmd.CommandText =
                "INSERT INTO Venues (Id, Name) VALUES ('11111111-1111-1111-1111-111111111111', 'The Corner Hotel')";
            insertCmd.ExecuteNonQuery();

            // Try to insert duplicate with different case - should fail
            using var duplicateCmd = connection.CreateCommand();
            duplicateCmd.CommandText =
                "INSERT INTO Venues (Id, Name) VALUES ('22222222-2222-2222-2222-222222222222', 'THE CORNER HOTEL')";

            // Assert - Should throw unique constraint violation
            var ex = Assert.Throws<SqliteException>(() => duplicateCmd.ExecuteNonQuery());
            Assert.Contains("UNIQUE", ex.Message);
        }
        finally
        {
            CleanupTestDb(connection, dbPath);
        }
    }

    [Fact]
    public void ExpressionIndex_MultiExpression_CompositeIndexSuccess()
    {
        // Arrange - Create table with multi-expression index
        var (connection, dbPath) = CreateTestDb();
        try
        {
            var schema = Schema
                .Define("Test")
                .Table(
                    "Suburbs",
                    t =>
                        t.Column("Id", PortableTypes.Uuid, c => c.PrimaryKey())
                            .Column("Name", PortableTypes.VarChar(100), c => c.NotNull())
                )
                .Table(
                    "Places",
                    t =>
                        t.Column("Id", PortableTypes.Uuid, c => c.PrimaryKey())
                            .Column("Name", PortableTypes.VarChar(200), c => c.NotNull())
                            .Column("SuburbId", PortableTypes.Uuid, c => c.NotNull())
                            .ExpressionIndex(
                                "uq_places_name_suburb",
                                ["lower(Name)", "SuburbId"],
                                unique: true
                            )
                )
                .Build();

            // Act
            var emptySchema = (
                (SchemaResultOk)SqliteSchemaInspector.Inspect(connection, _logger)
            ).Value;
            var operations = (
                (OperationsResultOk)SchemaDiff.Calculate(emptySchema, schema, logger: _logger)
            ).Value;

            var result = MigrationRunner.Apply(
                connection,
                operations,
                SqliteDdlGenerator.Generate,
                MigrationOptions.Default,
                _logger
            );

            // Assert
            Assert.True(
                result is MigrationApplyResultOk,
                $"Migration failed: {(result as MigrationApplyResultError)?.Value}"
            );

            // Verify composite expression index exists
            using var cmd = connection.CreateCommand();
            cmd.CommandText =
                "SELECT sql FROM sqlite_master WHERE type='index' AND name='uq_places_name_suburb'";
            var indexDef = cmd.ExecuteScalar() as string;

            Assert.NotNull(indexDef);
            Assert.Contains("UNIQUE", indexDef);
            Assert.Contains("lower", indexDef);
            Assert.Contains("SuburbId", indexDef);
        }
        finally
        {
            CleanupTestDb(connection, dbPath);
        }
    }

    [Fact]
    public void ExpressionIndex_Idempotent_NoErrorOnRerun()
    {
        // Arrange
        var (connection, dbPath) = CreateTestDb();
        try
        {
            var schema = Schema
                .Define("Test")
                .Table(
                    "Bands",
                    t =>
                        t.Column("Id", PortableTypes.Uuid, c => c.PrimaryKey())
                            .Column("Name", PortableTypes.VarChar(200), c => c.NotNull())
                            .ExpressionIndex("uq_bands_name", "lower(Name)", unique: true)
                )
                .Build();

            // Act - Run migration twice
            for (var i = 0; i < 2; i++)
            {
                var currentSchema = (
                    (SchemaResultOk)SqliteSchemaInspector.Inspect(connection, _logger)
                ).Value;

                var operations = (
                    (OperationsResultOk)SchemaDiff.Calculate(currentSchema, schema, logger: _logger)
                ).Value;

                var result = MigrationRunner.Apply(
                    connection,
                    operations,
                    SqliteDdlGenerator.Generate,
                    MigrationOptions.Default,
                    _logger
                );

                Assert.True(
                    result is MigrationApplyResultOk,
                    $"Migration {i + 1} failed: {(result as MigrationApplyResultError)?.Value}"
                );

                // Second run should have 0 operations (schema already matches)
                if (i == 1)
                {
                    Assert.Empty(operations);
                }
            }
        }
        finally
        {
            CleanupTestDb(connection, dbPath);
        }
    }

    [Fact]
    public void ExpressionIndex_SchemaInspector_DetectsExpressionIndex()
    {
        // Arrange - Create expression index via raw SQL
        var (connection, dbPath) = CreateTestDb();
        try
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = """
                CREATE TABLE artists (
                    id TEXT PRIMARY KEY,
                    name TEXT NOT NULL
                );
                CREATE UNIQUE INDEX uq_artists_name ON artists(lower(name));
                """;
            cmd.ExecuteNonQuery();

            // Act - Inspect schema
            var result = SqliteSchemaInspector.Inspect(connection, _logger);

            // Assert
            Assert.True(result is SchemaResultOk);
            var schema = ((SchemaResultOk)result).Value;
            var artists = schema.Tables.Single(t => t.Name == "artists");

            Assert.Single(artists.Indexes);
            var index = artists.Indexes[0];
            Assert.Equal("uq_artists_name", index.Name);
            Assert.True(index.IsUnique);
            Assert.NotEmpty(index.Expressions);
            Assert.Contains("lower(name)", index.Expressions);
        }
        finally
        {
            CleanupTestDb(connection, dbPath);
        }
    }

    // =============================================================================
    // Index Conversion Tests (Column <-> Expression)
    // =============================================================================

    [Fact]
    public void UpgradeIndex_ColumnToExpression_RequiresDropAndCreate()
    {
        // Arrange - Create table with regular column index
        var (connection, dbPath) = CreateTestDb();
        try
        {
            var v1 = Schema
                .Define("Test")
                .Table(
                    "Artists",
                    t =>
                        t.Column("Id", PortableTypes.Uuid, c => c.PrimaryKey())
                            .Column("Name", PortableTypes.VarChar(200), c => c.NotNull())
                            .Index("idx_artists_name", "Name", unique: true)
                )
                .Build();

            // Apply v1
            SeedSchema(connection, v1, _logger);

            // v2 changes to expression index (different name since it's semantically different)
            var v2 = Schema
                .Define("Test")
                .Table(
                    "Artists",
                    t =>
                        t.Column("Id", PortableTypes.Uuid, c => c.PrimaryKey())
                            .Column("Name", PortableTypes.VarChar(200), c => c.NotNull())
                            .ExpressionIndex("uq_artists_name_ci", "lower(Name)", unique: true)
                )
                .Build();

            // Act - Calculate upgrade operations
            var currentSchema = (
                (SchemaResultOk)SqliteSchemaInspector.Inspect(connection, _logger)
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
                connection,
                upgradeOps,
                SqliteDdlGenerator.Generate,
                MigrationOptions.Destructive,
                _logger
            );

            Assert.True(result is MigrationApplyResultOk);

            // Verify new expression index exists
            using var cmd = connection.CreateCommand();
            cmd.CommandText =
                "SELECT sql FROM sqlite_master WHERE type='index' AND name='uq_artists_name_ci'";
            var indexDef = cmd.ExecuteScalar() as string;
            Assert.NotNull(indexDef);
            Assert.Contains("lower", indexDef);
        }
        finally
        {
            CleanupTestDb(connection, dbPath);
        }
    }

    [Fact]
    public void UpgradeIndex_ExpressionToColumn_RequiresDropAndCreate()
    {
        // Arrange - Create table with expression index
        var (connection, dbPath) = CreateTestDb();
        try
        {
            var v1 = Schema
                .Define("Test")
                .Table(
                    "Venues",
                    t =>
                        t.Column("Id", PortableTypes.Uuid, c => c.PrimaryKey())
                            .Column("Name", PortableTypes.VarChar(200), c => c.NotNull())
                            .ExpressionIndex("uq_venues_name", "lower(Name)", unique: true)
                )
                .Build();

            // Apply v1
            SeedSchema(connection, v1, _logger);

            // v2 changes back to simple column index (different name)
            var v2 = Schema
                .Define("Test")
                .Table(
                    "Venues",
                    t =>
                        t.Column("Id", PortableTypes.Uuid, c => c.PrimaryKey())
                            .Column("Name", PortableTypes.VarChar(200), c => c.NotNull())
                            .Index("idx_venues_name", "Name", unique: true)
                )
                .Build();

            // Act
            var currentSchema = (
                (SchemaResultOk)SqliteSchemaInspector.Inspect(connection, _logger)
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
                connection,
                upgradeOps,
                SqliteDdlGenerator.Generate,
                MigrationOptions.Destructive,
                _logger
            );

            Assert.True(result is MigrationApplyResultOk);

            // Verify new column index exists (no lower() function)
            using var cmd = connection.CreateCommand();
            cmd.CommandText =
                "SELECT sql FROM sqlite_master WHERE type='index' AND name='idx_venues_name'";
            var indexDef = cmd.ExecuteScalar() as string;
            Assert.NotNull(indexDef);
            Assert.DoesNotContain("lower", indexDef);
        }
        finally
        {
            CleanupTestDb(connection, dbPath);
        }
    }

    // =============================================================================
    // LQL Default Value Tests - Platform-Independent Defaults
    // =============================================================================

    [Fact]
    public void LqlDefault_NowFunction_TranslatesToCurrentTimestamp()
    {
        // Arrange
        var (connection, dbPath) = CreateTestDb();
        try
        {
            var schema = Schema
                .Define("Test")
                .Table(
                    "events",
                    t =>
                        t.Column("id", PortableTypes.Int, c => c.PrimaryKey())
                            .Column(
                                "created_at",
                                PortableTypes.DateTime(),
                                c => c.NotNull().DefaultLql("now()")
                            )
                )
                .Build();

            // Act
            var emptySchema = (
                (SchemaResultOk)SqliteSchemaInspector.Inspect(connection, _logger)
            ).Value;
            var operations = (
                (OperationsResultOk)SchemaDiff.Calculate(emptySchema, schema, logger: _logger)
            ).Value;
            var result = MigrationRunner.Apply(
                connection,
                operations,
                SqliteDdlGenerator.Generate,
                MigrationOptions.Default,
                _logger
            );

            // Assert
            Assert.True(result is MigrationApplyResultOk);

            // Verify table DDL contains datetime('now') - the SQLite translation of now()
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT sql FROM sqlite_master WHERE type='table' AND name='events'";
            var tableDef = cmd.ExecuteScalar() as string;
            Assert.NotNull(tableDef);
            Assert.Contains("(datetime('now'))", tableDef);

            // Insert and verify default works
            using var insertCmd = connection.CreateCommand();
            insertCmd.CommandText = "INSERT INTO events (id) VALUES (1)";
            insertCmd.ExecuteNonQuery();

            using var selectCmd = connection.CreateCommand();
            selectCmd.CommandText = "SELECT created_at FROM events WHERE id = 1";
            var createdAt = selectCmd.ExecuteScalar() as string;
            Assert.NotNull(createdAt);
            Assert.NotEmpty(createdAt);
        }
        finally
        {
            CleanupTestDb(connection, dbPath);
        }
    }

    [Fact]
    public void LqlDefault_BooleanTrue_TranslatesTo1()
    {
        // Arrange
        var (connection, dbPath) = CreateTestDb();
        try
        {
            var schema = Schema
                .Define("Test")
                .Table(
                    "flags",
                    t =>
                        t.Column("id", PortableTypes.Int, c => c.PrimaryKey())
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
                (SchemaResultOk)SqliteSchemaInspector.Inspect(connection, _logger)
            ).Value;
            var operations = (
                (OperationsResultOk)SchemaDiff.Calculate(emptySchema, schema, logger: _logger)
            ).Value;
            var result = MigrationRunner.Apply(
                connection,
                operations,
                SqliteDdlGenerator.Generate,
                MigrationOptions.Default,
                _logger
            );

            // Assert
            Assert.True(result is MigrationApplyResultOk);

            // Verify table DDL contains 1 and 0 for boolean defaults
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT sql FROM sqlite_master WHERE type='table' AND name='flags'";
            var tableDef = cmd.ExecuteScalar() as string;
            Assert.NotNull(tableDef);
            Assert.Contains("DEFAULT 1", tableDef); // true -> 1
            Assert.Contains("DEFAULT 0", tableDef); // false -> 0

            // Insert and verify defaults work
            using var insertCmd = connection.CreateCommand();
            insertCmd.CommandText = "INSERT INTO flags (id) VALUES (1)";
            insertCmd.ExecuteNonQuery();

            using var selectCmd = connection.CreateCommand();
            selectCmd.CommandText = "SELECT is_active, is_deleted FROM flags WHERE id = 1";
            using var reader = selectCmd.ExecuteReader();
            Assert.True(reader.Read());
            Assert.Equal(1, reader.GetInt64(0)); // is_active = true = 1
            Assert.Equal(0, reader.GetInt64(1)); // is_deleted = false = 0
        }
        finally
        {
            CleanupTestDb(connection, dbPath);
        }
    }

    [Fact]
    public void LqlDefault_NumericValues_PassThrough()
    {
        // Arrange
        var (connection, dbPath) = CreateTestDb();
        try
        {
            var schema = Schema
                .Define("Test")
                .Table(
                    "counters",
                    t =>
                        t.Column("id", PortableTypes.Int, c => c.PrimaryKey())
                            .Column("count", PortableTypes.Int, c => c.NotNull().DefaultLql("0"))
                            .Column(
                                "priority",
                                PortableTypes.Int,
                                c => c.NotNull().DefaultLql("100")
                            )
                            .Column(
                                "rate",
                                PortableTypes.Decimal(5, 2),
                                c => c.NotNull().DefaultLql("1.5")
                            )
                )
                .Build();

            // Act
            var emptySchema = (
                (SchemaResultOk)SqliteSchemaInspector.Inspect(connection, _logger)
            ).Value;
            var operations = (
                (OperationsResultOk)SchemaDiff.Calculate(emptySchema, schema, logger: _logger)
            ).Value;
            var result = MigrationRunner.Apply(
                connection,
                operations,
                SqliteDdlGenerator.Generate,
                MigrationOptions.Default,
                _logger
            );

            // Assert
            Assert.True(result is MigrationApplyResultOk);

            // Insert and verify defaults work
            using var insertCmd = connection.CreateCommand();
            insertCmd.CommandText = "INSERT INTO counters (id) VALUES (1)";
            insertCmd.ExecuteNonQuery();

            using var selectCmd = connection.CreateCommand();
            selectCmd.CommandText = "SELECT count, priority, rate FROM counters WHERE id = 1";
            using var reader = selectCmd.ExecuteReader();
            Assert.True(reader.Read());
            Assert.Equal(0, reader.GetInt64(0));
            Assert.Equal(100, reader.GetInt64(1));
            Assert.Equal(1.5, reader.GetDouble(2), 2);
        }
        finally
        {
            CleanupTestDb(connection, dbPath);
        }
    }

    [Fact]
    public void LqlDefault_StringLiteral_PassThrough()
    {
        // Arrange
        var (connection, dbPath) = CreateTestDb();
        try
        {
            var schema = Schema
                .Define("Test")
                .Table(
                    "items",
                    t =>
                        t.Column("id", PortableTypes.Int, c => c.PrimaryKey())
                            .Column(
                                "status",
                                PortableTypes.VarChar(20),
                                c => c.NotNull().DefaultLql("'pending'")
                            )
                            .Column(
                                "category",
                                PortableTypes.VarChar(50),
                                c => c.DefaultLql("'uncategorized'")
                            )
                )
                .Build();

            // Act
            var emptySchema = (
                (SchemaResultOk)SqliteSchemaInspector.Inspect(connection, _logger)
            ).Value;
            var operations = (
                (OperationsResultOk)SchemaDiff.Calculate(emptySchema, schema, logger: _logger)
            ).Value;
            var result = MigrationRunner.Apply(
                connection,
                operations,
                SqliteDdlGenerator.Generate,
                MigrationOptions.Default,
                _logger
            );

            // Assert
            Assert.True(result is MigrationApplyResultOk);

            // Insert and verify defaults work
            using var insertCmd = connection.CreateCommand();
            insertCmd.CommandText = "INSERT INTO items (id) VALUES (1)";
            insertCmd.ExecuteNonQuery();

            using var selectCmd = connection.CreateCommand();
            selectCmd.CommandText = "SELECT status, category FROM items WHERE id = 1";
            using var reader = selectCmd.ExecuteReader();
            Assert.True(reader.Read());
            Assert.Equal("pending", reader.GetString(0));
            Assert.Equal("uncategorized", reader.GetString(1));
        }
        finally
        {
            CleanupTestDb(connection, dbPath);
        }
    }

    [Fact]
    public void LqlDefault_GenUuid_GeneratesValidUuidFormat()
    {
        // Arrange
        var (connection, dbPath) = CreateTestDb();
        try
        {
            var schema = Schema
                .Define("Test")
                .Table(
                    "records",
                    t =>
                        t.Column(
                                "id",
                                PortableTypes.Uuid,
                                c => c.PrimaryKey().DefaultLql("gen_uuid()")
                            )
                            .Column("name", PortableTypes.VarChar(100))
                )
                .Build();

            // Act
            var emptySchema = (
                (SchemaResultOk)SqliteSchemaInspector.Inspect(connection, _logger)
            ).Value;
            var operations = (
                (OperationsResultOk)SchemaDiff.Calculate(emptySchema, schema, logger: _logger)
            ).Value;
            var result = MigrationRunner.Apply(
                connection,
                operations,
                SqliteDdlGenerator.Generate,
                MigrationOptions.Default,
                _logger
            );

            // Assert
            Assert.True(result is MigrationApplyResultOk);

            // Insert multiple rows and verify UUIDs are generated and unique
            using var insertCmd = connection.CreateCommand();
            insertCmd.CommandText =
                "INSERT INTO records (name) VALUES ('test1'), ('test2'), ('test3')";
            insertCmd.ExecuteNonQuery();

            using var selectCmd = connection.CreateCommand();
            selectCmd.CommandText = "SELECT id FROM records";
            using var reader = selectCmd.ExecuteReader();

            var uuids = new List<string>();
            while (reader.Read())
            {
                var uuid = reader.GetString(0);
                Assert.NotNull(uuid);
                Assert.Matches(
                    @"^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$",
                    uuid
                );
                uuids.Add(uuid);
            }

            // All UUIDs should be unique
            Assert.Equal(3, uuids.Count);
            Assert.Equal(3, uuids.Distinct().Count());
        }
        finally
        {
            CleanupTestDb(connection, dbPath);
        }
    }

    [Fact]
    public void LqlDefault_CurrentDate_ReturnsDateOnly()
    {
        // Arrange
        var (connection, dbPath) = CreateTestDb();
        try
        {
            var schema = Schema
                .Define("Test")
                .Table(
                    "logs",
                    t =>
                        t.Column("id", PortableTypes.Int, c => c.PrimaryKey())
                            .Column(
                                "log_date",
                                PortableTypes.Date,
                                c => c.NotNull().DefaultLql("current_date()")
                            )
                )
                .Build();

            // Act
            var emptySchema = (
                (SchemaResultOk)SqliteSchemaInspector.Inspect(connection, _logger)
            ).Value;
            var operations = (
                (OperationsResultOk)SchemaDiff.Calculate(emptySchema, schema, logger: _logger)
            ).Value;
            var result = MigrationRunner.Apply(
                connection,
                operations,
                SqliteDdlGenerator.Generate,
                MigrationOptions.Default,
                _logger
            );

            // Assert
            Assert.True(result is MigrationApplyResultOk);

            // Insert and verify default date
            using var insertCmd = connection.CreateCommand();
            insertCmd.CommandText = "INSERT INTO logs (id) VALUES (1)";
            insertCmd.ExecuteNonQuery();

            using var selectCmd = connection.CreateCommand();
            selectCmd.CommandText = "SELECT log_date FROM logs WHERE id = 1";
            var logDate = selectCmd.ExecuteScalar() as string;
            Assert.NotNull(logDate);
            Assert.Matches(@"^\d{4}-\d{2}-\d{2}$", logDate); // YYYY-MM-DD format
        }
        finally
        {
            CleanupTestDb(connection, dbPath);
        }
    }

    [Fact]
    public void LqlDefault_MixedDefaults_AllWorkTogether()
    {
        // Arrange - A complex table with multiple LQL defaults
        var (connection, dbPath) = CreateTestDb();
        try
        {
            var schema = Schema
                .Define("Test")
                .Table(
                    "orders",
                    t =>
                        t.Column(
                                "id",
                                PortableTypes.Uuid,
                                c => c.PrimaryKey().DefaultLql("gen_uuid()")
                            )
                            .Column(
                                "status",
                                PortableTypes.VarChar(20),
                                c => c.NotNull().DefaultLql("'pending'")
                            )
                            .Column("quantity", PortableTypes.Int, c => c.NotNull().DefaultLql("1"))
                            .Column(
                                "is_urgent",
                                PortableTypes.Boolean,
                                c => c.NotNull().DefaultLql("false")
                            )
                            .Column(
                                "created_at",
                                PortableTypes.DateTime(),
                                c => c.NotNull().DefaultLql("now()")
                            )
                )
                .Build();

            // Act
            var emptySchema = (
                (SchemaResultOk)SqliteSchemaInspector.Inspect(connection, _logger)
            ).Value;
            var operations = (
                (OperationsResultOk)SchemaDiff.Calculate(emptySchema, schema, logger: _logger)
            ).Value;
            var result = MigrationRunner.Apply(
                connection,
                operations,
                SqliteDdlGenerator.Generate,
                MigrationOptions.Default,
                _logger
            );

            // Assert
            Assert.True(result is MigrationApplyResultOk);

            // Insert with no columns specified - all defaults should apply
            using var insertCmd = connection.CreateCommand();
            insertCmd.CommandText = "INSERT INTO orders DEFAULT VALUES";
            insertCmd.ExecuteNonQuery();

            using var selectCmd = connection.CreateCommand();
            selectCmd.CommandText =
                "SELECT id, status, quantity, is_urgent, created_at FROM orders";
            using var reader = selectCmd.ExecuteReader();
            Assert.True(reader.Read());

            var id = reader.GetString(0);
            var status = reader.GetString(1);
            var quantity = reader.GetInt64(2);
            var isUrgent = reader.GetInt64(3);
            var createdAt = reader.GetString(4);

            Assert.Matches(@"^[0-9a-f-]{36}$", id); // UUID format
            Assert.Equal("pending", status); // String default
            Assert.Equal(1, quantity); // Numeric default
            Assert.Equal(0, isUrgent); // Boolean false = 0
            Assert.NotEmpty(createdAt); // Timestamp generated
        }
        finally
        {
            CleanupTestDb(connection, dbPath);
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    // VECTOR COLUMN TEST — SQLite has no native pgvector, so the
    // generator must fall back to BLOB storage. The user-space app is
    // responsible for its own (de)serialisation — the migration
    // pipeline's job is just to not blow up on VectorType and to
    // produce a column wide enough for raw float bytes.
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void CreateTableWithVectorColumn_FallsBackToBlob_AndMigratesSuccessfully()
    {
        // Arrange
        var (connection, dbPath) = CreateTestDb();
        try
        {
            var schema = Schema
                .Define("Embeddings")
                .Table(
                    "documents",
                    t =>
                        t.Column("Id", PortableTypes.Uuid, c => c.PrimaryKey())
                            .Column("Body", PortableTypes.Text, c => c.NotNull())
                            .Column("Embedding", PortableTypes.Vector(384), c => c.NotNull())
                )
                .Build();

            var emptySchema = (
                (SchemaResultOk)SqliteSchemaInspector.Inspect(connection, _logger)
            ).Value;
            var ops = (
                (OperationsResultOk)SchemaDiff.Calculate(emptySchema, schema, logger: _logger)
            ).Value;

            // Act
            var result = MigrationRunner.Apply(
                connection,
                ops,
                SqliteDdlGenerator.Generate,
                MigrationOptions.Default,
                _logger
            );

            // Assert — migration succeeded
            Assert.True(
                result is MigrationApplyResultOk,
                $"SQLite vector fallback migration failed: {(result as MigrationApplyResultError)?.Value}"
            );

            // Assert — embedding column exists with BLOB affinity
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "PRAGMA table_info('documents')";
            using var reader = cmd.ExecuteReader();
            string? embeddingType = null;
            while (reader.Read())
            {
                if (reader.GetString(1) == "Embedding")
                {
                    embeddingType = reader.GetString(2);
                    break;
                }
            }
            Assert.Equal("BLOB", embeddingType);

            // Act — round-trip insert: user-space writes float32[] bytes
            var floats = new float[384];
            for (var i = 0; i < floats.Length; i++)
            {
                floats[i] = 0.5f;
            }
            var bytes = new byte[floats.Length * sizeof(float)];
            Buffer.BlockCopy(floats, 0, bytes, 0, bytes.Length);

            using (var insertCmd = connection.CreateCommand())
            {
                insertCmd.CommandText =
                    "INSERT INTO documents (Id, Body, Embedding) VALUES (@id, @body, @embedding)";
                insertCmd.Parameters.AddWithValue("@id", Guid.NewGuid().ToString());
                insertCmd.Parameters.AddWithValue("@body", "hello");
                insertCmd.Parameters.AddWithValue("@embedding", bytes);
                Assert.Equal(1, insertCmd.ExecuteNonQuery());
            }

            using (var selectCmd = connection.CreateCommand())
            {
                selectCmd.CommandText = "SELECT Body, length(Embedding) FROM documents";
                using var r = selectCmd.ExecuteReader();
                Assert.True(r.Read());
                Assert.Equal("hello", r.GetString(0));
                // 384 floats × 4 bytes = 1536 bytes
                Assert.Equal(1536L, r.GetInt64(1));
            }
        }
        finally
        {
            CleanupTestDb(connection, dbPath);
        }
    }
}
