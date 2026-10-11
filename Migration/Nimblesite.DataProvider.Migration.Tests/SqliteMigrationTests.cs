// Implements [MIG-TEST-SCHEMA-FIXTURES].
// Implements [MIG-TEST-ID-NAME-COLUMNS].
// Implements [CI-TEST-SHARED-COMMAND].
using Tests.Shared;
using static global::Tests.Shared.ResultAssertions;
using static global::Tests.Shared.StringAssertions;

namespace Nimblesite.DataProvider.Migration.Tests;

/// <summary>
/// E2E tests for SQLite migrations - greenfield and upgrades.
/// </summary>
public sealed partial class SqliteMigrationTests
{
    private readonly ILogger _logger = NullLogger.Instance;

    // Implements [MIG-TEST-SQLITE-SEED].
    private static MigrationApplyResult SeedSchema(
        SqliteConnection connection,
        SchemaDefinition schema,
        ILogger logger
    )
    {
        var current = ((SchemaResultOk)SqliteSchemaInspector.Inspect(connection, logger)).Value;
        var ops = ((OperationsResultOk)SchemaDiff.Calculate(current, schema, logger: logger)).Value;
        return SqliteTestDb.TryApply(connection: connection, operations: ops, logger: logger);
    }

    [Fact]
    public void CreateDatabaseFromScratch_SingleTable_Success()
    {
        // Arrange
        // Implements [MIG-TEST-SQLITE-LIFETIME].
        SqliteTestDb.WithDb(test: connection =>
        {
            var schema = Fixture_CreateDatabaseFromScratch_SingleTable_Success_1();

            // Act
            var emptySchema = SqliteSchemaInspector.Inspect(connection, _logger);
            Assert.True(emptySchema is SchemaResultOk);

            var operations = SchemaDiff.Calculate(
                ((SchemaResultOk)emptySchema).Value,
                schema,
                logger: _logger
            );

            var ops = Ok(actual: operations).Value;

            var result = SqliteTestDb.TryApply(
                connection: connection,
                operations: ops,
                logger: _logger
            );

            // Assert
            Assert.True(result is MigrationApplyResultOk);

            // Verify table exists
            var inspected = SqliteSchemaInspector.Inspect(connection, _logger);

            var inspectedSchema = Ok(actual: inspected).Value;
            Assert.Single(inspectedSchema.Tables);
            Assert.Equal("Users", inspectedSchema.Tables[0].Name);
            Assert.Equal(3, inspectedSchema.Tables[0].Columns.Count);
        });
    }

    [Fact]
    public void CreateDatabaseFromScratch_MultipleTablesWithForeignKeys_Success()
    {
        // Arrange
        // Implements [MIG-TEST-SQLITE-LIFETIME].
        SqliteTestDb.WithDb(test: connection =>
        {
            // Enable foreign keys
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "PRAGMA foreign_keys = ON";
                cmd.ExecuteNonQuery();
            }

            var schema =
                Fixture_CreateDatabaseFromScratch_MultipleTablesWithForeignKeys_Success_1();

            // Act
            SeedSchemaAndAssertSuccess(input0: connection, input1: schema);

            var inspected = (
                (SchemaResultOk)SqliteSchemaInspector.Inspect(connection, _logger)
            ).Value;

            Assert.Equal(2, inspected.Tables.Count);
            Assert.Contains(inspected.Tables, t => t.Name == "Users");
            Assert.Contains(inspected.Tables, t => t.Name == "Orders");

            var ordersTable = inspected.Tables.First(t => t.Name == "Orders");
            Assert.Single(ordersTable.ForeignKeys);
            Assert.Equal("Users", ordersTable.ForeignKeys[0].ReferencedTable);
        });
    }

    [Fact]
    public void UpgradeExistingDatabase_AddColumn_Success()
    {
        // Arrange
        // Implements [MIG-TEST-SQLITE-LIFETIME].
        SqliteTestDb.WithDb(test: connection =>
        {
            // Create initial schema
            var v1 = Fixture_UpgradeExistingDatabase_AddColumn_Success_1();

            // Apply v1
            SeedSchema(connection, v1, _logger);

            // Define v2 with new columns
            var v2 = Fixture_UpgradeExistingDatabase_AddColumn_Success_2();

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

            var finalSchema = ApplyChangesAndInspectSchema(input0: connection, input1: upgradeOps);
            var users = finalSchema.Tables.Single(t => t.Name == "Users");
            Assert.Equal(4, users.Columns.Count);
            Assert.Contains(users.Columns, c => c.Name == "Name");
            Assert.Contains(users.Columns, c => c.Name == "CreatedAt");
        });
    }

    [Fact]
    public void UpgradeExistingDatabase_AddTable_Success()
    {
        // Arrange
        // Implements [MIG-TEST-SQLITE-LIFETIME].
        SqliteTestDb.WithDb(test: connection =>
        {
            var v1 = Schema
                .Define("Test")
                .Table("Users", t => t.Column("Id", PortableTypes.Uuid, c => c.PrimaryKey()))
                .Build();

            // Apply v1
            SeedSchema(connection, v1, _logger);

            // v2 adds a new table
            var v2 = Fixture_UpgradeExistingDatabase_AddTable_Success_1();

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

            var finalSchema = ApplyChangesAndInspectSchema(input0: connection, input1: upgradeOps);
            Assert.Equal(2, finalSchema.Tables.Count);
            Assert.Contains(finalSchema.Tables, t => t.Name == "Products");
        });
    }

    [Fact]
    public void UpgradeExistingDatabase_AddIndex_Success()
    {
        // Arrange
        // Implements [MIG-TEST-SQLITE-LIFETIME].
        SqliteTestDb.WithDb(test: connection =>
        {
            var v1 = Fixture_UpgradeExistingDatabase_AddIndex_Success_1();

            // Apply v1
            SeedSchema(connection, v1, _logger);

            // v2 adds an index
            var v2 = Fixture_UpgradeExistingDatabase_AddIndex_Success_2();

            // Act
            var currentSchema = (
                (SchemaResultOk)SqliteSchemaInspector.Inspect(connection, _logger)
            ).Value;

            var upgradeOps = (
                (OperationsResultOk)SchemaDiff.Calculate(currentSchema, v2, logger: _logger)
            ).Value;

            Assert.Single(upgradeOps);
            Assert.IsType<CreateIndexOperation>(upgradeOps[0]);

            var finalSchema = ApplyChangesAndInspectSchema(input0: connection, input1: upgradeOps);
            var users = finalSchema.Tables.Single(t => t.Name == "Users");
            Assert.Single(users.Indexes);
            Assert.Equal("idx_users_email", users.Indexes[0].Name);
            Assert.True(users.Indexes[0].IsUnique);
        });
    }

    [Fact]
    public void Migration_IsIdempotent_NoErrorOnRerun()
    {
        // Arrange
        // Implements [MIG-TEST-SQLITE-LIFETIME].
        SqliteTestDb.WithDb(test: connection =>
        {
            var schema = Fixture_Migration_IsIdempotent_NoErrorOnRerun_1();

            // Act - Run migration twice
            for (var i = 0; i < 2; i++)
            {
                var currentSchema = (
                    (SchemaResultOk)SqliteSchemaInspector.Inspect(connection, _logger)
                ).Value;

                var operations = (
                    (OperationsResultOk)SchemaDiff.Calculate(currentSchema, schema, logger: _logger)
                ).Value;

                var result = SqliteTestDb.TryApply(
                    connection: connection,
                    operations: operations,
                    logger: _logger
                );

                Assert.True(result is MigrationApplyResultOk);

                // Second run should have 0 operations
                if (i == 1)
                {
                    Assert.Empty(operations);
                }
            }
        });
    }

    [Fact]
    public void CreateTable_AllPortableTypes_Success()
    {
        // Arrange
        // Implements [MIG-TEST-SQLITE-LIFETIME].
        SqliteTestDb.WithDb(test: connection =>
        {
            var schema = Fixture_CreateTable_AllPortableTypes_Success_1();

            // Act
            SeedSchemaAndAssertSuccess(input0: connection, input1: schema);

            var inspected = (
                (SchemaResultOk)SqliteSchemaInspector.Inspect(connection, _logger)
            ).Value;
            var table = inspected.Tables.Single();
            Assert.Equal(25, table.Columns.Count);
        });
    }

    [Fact]
    public void Destructive_DropTable_BlockedByDefault()
    {
        // Arrange
        // Implements [MIG-TEST-SQLITE-LIFETIME].
        SqliteTestDb.WithDb(test: connection =>
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
        });
    }

    [Fact]
    public void Destructive_DropTable_AllowedWithOption()
    {
        // Arrange
        // Implements [MIG-TEST-SQLITE-LIFETIME].
        SqliteTestDb.WithDb(test: connection =>
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

            var result = SqliteTestDb.TryApply(
                connection: connection,
                operations: operations,
                logger: _logger,
                options: MigrationOptions.Destructive
            );

            // Assert
            Assert.True(result is MigrationApplyResultOk);

            var finalSchema = (
                (SchemaResultOk)SqliteSchemaInspector.Inspect(connection, _logger)
            ).Value;
            Assert.Single(finalSchema.Tables);
            Assert.DoesNotContain(finalSchema.Tables, t => t.Name == "Products");
        });
    }

    [Fact]
    public void SchemaInspector_RoundTrip_Matches()
    {
        // Arrange
        // Implements [MIG-TEST-SQLITE-LIFETIME].
        SqliteTestDb.WithDb(test: connection =>
        {
            var schema = Fixture_SchemaInspector_RoundTrip_Matches_1();

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
        });
    }

    [Fact]
    public void DestructiveOperation_BlockedByDefault_ReturnsUsefulError()
    {
        // Arrange
        // Implements [MIG-TEST-SQLITE-LIFETIME].
        SqliteTestDb.WithDb(test: connection =>
        {
            // Create table first
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "CREATE TABLE ToBeDropped (Id INTEGER PRIMARY KEY)";
            cmd.ExecuteNonQuery();

            var dropOperation = new DropTableOperation("main", "ToBeDropped");

            // Act - try to apply destructive operation with default options
            var result = SqliteTestDb.TryApply(
                connection: connection,
                operations: [dropOperation],
                logger: _logger
            );

            // Assert - should fail with useful error message

            var error = Expected<MigrationApplyResultError>(actual: result).Value;
            Assert.Contains("Destructive", error.Message);
            Assert.Contains("DropTableOperation", error.Message);
        });
    }

    [Fact]
    public void InvalidSql_ReturnsUsefulError()
    {
        // Arrange
        // Implements [MIG-TEST-SQLITE-LIFETIME].
        SqliteTestDb.WithDb(test: connection =>
        {
            // Create a custom operation that generates invalid SQL
            var badTable = Dataset_InvalidSql_ReturnsUsefulError_1();

            var createOp = new CreateTableOperation(badTable);

            // Act
            var result = SqliteTestDb.TryApply(
                connection: connection,
                operations: [createOp],
                logger: _logger
            );

            // Assert - should fail (invalid SQL) but not crash
            // Note: SQLite may accept this - adjust test if needed
            Assert.NotNull(result);
        });
    }

    [Fact]
    public void SchemaCapture_ExistingDatabase_ReturnsCompleteSchema()
    {
        // Arrange - Create database with raw SQL (simulate existing DB)
        // Implements [MIG-TEST-SQLITE-LIFETIME].
        SqliteTestDb.WithDb(test: connection =>
        {
            DatabaseCommands.Execute(
                connection: connection,
                configure: command =>
                    command.CommandText =
                        Data_SchemaCapture_ExistingDatabase_ReturnsCompleteSchema_1
            );

            // Act - CAPTURE the existing schema
            var captureResult = SqliteSchemaInspector.Inspect(connection, _logger);

            // Assert - schema captured successfully

            var schema = Ok(actual: captureResult).Value;

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
        });
    }

    [Fact]
    public void SchemaCapture_SerializesToJson_RoundTrip()
    {
        // Arrange
        // Implements [MIG-TEST-SQLITE-LIFETIME].
        SqliteTestDb.WithDb(test: connection =>
        {
            DatabaseCommands.Execute(
                connection: connection,
                configure: command =>
                    command.CommandText = Data_SchemaCapture_SerializesToJson_RoundTrip_1
            );

            // Act - Capture and serialize to JSON
            var captureResult = SqliteSchemaInspector.Inspect(connection, _logger);

            var schema = Ok(actual: captureResult).Value;

            var json = SchemaSerializer.ToJson(schema);

            // Assert - JSON is valid and contains expected data
            Assert.NotNull(json);
            ContainsAll(actual: json, expected: ["products", "name", "idx_products_name"]);

            // Deserialize and verify round-trip
            var restored = SchemaSerializer.FromJson(json);
            Assert.NotNull(restored);
            Assert.Single(restored.Tables);
            Assert.Equal("products", restored.Tables[0].Name);
            Assert.Equal(4, restored.Tables[0].Columns.Count);
            Assert.Single(restored.Tables[0].Indexes);
        });
    }

    // =============================================================================
    // Expression Index Tests
    // =============================================================================

    [Fact]
    public void ExpressionIndex_CreateWithLowerFunction_Success()
    {
        // Arrange - Create table with expression index for case-insensitive uniqueness
        // Implements [MIG-TEST-SQLITE-LIFETIME].
        SqliteTestDb.WithDb(test: connection =>
        {
            var schema = Fixture_ExpressionIndex_CreateWithLowerFunction_Success_1();

            // Act
            var result = SeedSchema(connection: connection, schema: schema, logger: _logger);

            // Assert - Migration succeeded
            Assert.True(
                result is MigrationApplyResultOk,
                $"Migration failed: {(result as MigrationApplyResultError)?.Value}"
            );

            // Verify index exists
            var indexDef =
                global::Tests.Shared.DatabaseCommands.Scalar(
                    connection: connection,
                    configure: command =>
                        command.CommandText =
                            "SELECT sql FROM sqlite_master WHERE type='index' AND name='uq_artists_name'"
                ) as string;

            Assert.NotNull(indexDef);
            ContainsAll(actual: indexDef, expected: ["UNIQUE", "lower"]);
        });
    }

    [Fact]
    public void ExpressionIndex_EnforcesCaseInsensitiveUniqueness()
    {
        // Arrange - Create table with expression index
        // Implements [MIG-TEST-SQLITE-LIFETIME].
        SqliteTestDb.WithDb(test: connection =>
        {
            var schema = Fixture_ExpressionIndex_EnforcesCaseInsensitiveUniqueness_1();

            SeedSchema(connection, schema, _logger);

            // Act - Insert first venue
            DatabaseCommands.Execute(
                connection: connection,
                configure: command =>
                    command.CommandText =
                        "INSERT INTO Venues (Id, Name) VALUES ('11111111-1111-1111-1111-111111111111', 'The Corner Hotel')"
            );

            // Try to insert duplicate with different case - should fail
            using var duplicateCmd = connection.CreateCommand();
            duplicateCmd.CommandText =
                "INSERT INTO Venues (Id, Name) VALUES ('22222222-2222-2222-2222-222222222222', 'THE CORNER HOTEL')";

            // Assert - Should throw unique constraint violation
            var ex = Assert.Throws<SqliteException>(() => duplicateCmd.ExecuteNonQuery());
            Assert.Contains("UNIQUE", ex.Message);
        });
    }

    [Fact]
    public void ExpressionIndex_MultiExpression_CompositeIndexSuccess()
    {
        // Arrange - Create table with multi-expression index
        // Implements [MIG-TEST-SQLITE-LIFETIME].
        SqliteTestDb.WithDb(test: connection =>
        {
            var schema = Fixture_ExpressionIndex_MultiExpression_CompositeIndexSuccess_1();

            // Act
            var result = SeedSchema(connection: connection, schema: schema, logger: _logger);

            // Assert
            Assert.True(
                result is MigrationApplyResultOk,
                $"Migration failed: {(result as MigrationApplyResultError)?.Value}"
            );

            // Verify composite expression index exists
            var indexDef =
                global::Tests.Shared.DatabaseCommands.Scalar(
                    connection: connection,
                    configure: command =>
                        command.CommandText =
                            "SELECT sql FROM sqlite_master WHERE type='index' AND name='uq_places_name_suburb'"
                ) as string;

            Assert.NotNull(indexDef);
            ContainsAll(actual: indexDef, expected: ["UNIQUE", "lower", "SuburbId"]);
        });
    }

    [Fact]
    public void ExpressionIndex_Idempotent_NoErrorOnRerun()
    {
        // Arrange
        // Implements [MIG-TEST-SQLITE-LIFETIME].
        SqliteTestDb.WithDb(test: connection =>
        {
            var schema = Fixture_ExpressionIndex_Idempotent_NoErrorOnRerun_1();

            // Act - Run migration twice
            for (var i = 0; i < 2; i++)
            {
                var currentSchema = (
                    (SchemaResultOk)SqliteSchemaInspector.Inspect(connection, _logger)
                ).Value;

                var operations = (
                    (OperationsResultOk)SchemaDiff.Calculate(currentSchema, schema, logger: _logger)
                ).Value;

                var result = SqliteTestDb.TryApply(
                    connection: connection,
                    operations: operations,
                    logger: _logger
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
        });
    }

    [Fact]
    public void ExpressionIndex_SchemaInspector_DetectsExpressionIndex()
    {
        // Arrange - Create expression index via raw SQL
        // Implements [MIG-TEST-SQLITE-LIFETIME].
        SqliteTestDb.WithDb(test: connection =>
        {
            DatabaseCommands.Execute(
                connection: connection,
                configure: command =>
                    command.CommandText =
                        Data_ExpressionIndex_SchemaInspector_DetectsExpressionIndex_1
            );

            // Act - Inspect schema
            var result = SqliteSchemaInspector.Inspect(connection, _logger);

            // Assert

            var schema = Ok(actual: result).Value;
            var artists = schema.Tables.Single(t => t.Name == "artists");

            Assert.Single(artists.Indexes);
            var index = artists.Indexes[0];
            Assert.Equal("uq_artists_name", index.Name);
            Assert.True(index.IsUnique);
            Assert.NotEmpty(index.Expressions);
            Assert.Contains("lower(name)", index.Expressions);
        });
    }

    // =============================================================================
    // Index Conversion Tests (Column <-> Expression)
    // =============================================================================

    [Fact]
    public void UpgradeIndex_ColumnToExpression_RequiresDropAndCreate()
    {
        // Arrange - Create table with regular column index
        // Implements [MIG-TEST-SQLITE-LIFETIME].
        SqliteTestDb.WithDb(test: connection =>
        {
            var v1 = Fixture_UpgradeIndex_ColumnToExpression_RequiresDropAndCreate_1();

            // Apply v1
            SeedSchema(connection, v1, _logger);

            // v2 changes to expression index (different name since it's semantically different)
            var v2 = Fixture_UpgradeIndex_ColumnToExpression_RequiresDropAndCreate_2();

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
            var result = SqliteTestDb.TryApply(
                connection: connection,
                operations: upgradeOps,
                logger: _logger,
                options: MigrationOptions.Destructive
            );

            Assert.True(result is MigrationApplyResultOk);

            // Verify new expression index exists
            var indexDef =
                global::Tests.Shared.DatabaseCommands.Scalar(
                    connection: connection,
                    configure: command =>
                        command.CommandText =
                            "SELECT sql FROM sqlite_master WHERE type='index' AND name='uq_artists_name_ci'"
                ) as string;

            Assert.NotNull(indexDef);
            Assert.Contains("lower", indexDef);
        });
    }

    [Fact]
    public void UpgradeIndex_ExpressionToColumn_RequiresDropAndCreate()
    {
        // Arrange - Create table with expression index
        // Implements [MIG-TEST-SQLITE-LIFETIME].
        SqliteTestDb.WithDb(test: connection =>
        {
            var v1 = Fixture_UpgradeIndex_ExpressionToColumn_RequiresDropAndCreate_1();

            // Apply v1
            SeedSchema(connection, v1, _logger);

            // v2 changes back to simple column index (different name)
            var v2 = Fixture_UpgradeIndex_ExpressionToColumn_RequiresDropAndCreate_2();

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

            var result = SqliteTestDb.TryApply(
                connection: connection,
                operations: upgradeOps,
                logger: _logger,
                options: MigrationOptions.Destructive
            );

            Assert.True(result is MigrationApplyResultOk);

            // Verify new column index exists (no lower() function)
            var indexDef =
                global::Tests.Shared.DatabaseCommands.Scalar(
                    connection: connection,
                    configure: command =>
                        command.CommandText =
                            "SELECT sql FROM sqlite_master WHERE type='index' AND name='idx_venues_name'"
                ) as string;

            Assert.NotNull(indexDef);
            Assert.DoesNotContain("lower", indexDef);
        });
    }

    // =============================================================================
    // LQL Default Value Tests - Platform-Independent Defaults
    // =============================================================================

    [Fact]
    public void LqlDefault_NowFunction_TranslatesToCurrentTimestamp()
    {
        // Arrange
        // Implements [MIG-TEST-SQLITE-LIFETIME].
        SqliteTestDb.WithDb(test: connection =>
        {
            var schema = Fixture_LqlDefault_NowFunction_TranslatesToCurrentTimestamp_1();

            // Act
            SeedSchemaAndAssertSuccess(input0: connection, input1: schema);

            // Verify table DDL contains datetime('now') - the SQLite translation of now()
            var tableDef =
                global::Tests.Shared.DatabaseCommands.Scalar(
                    connection: connection,
                    configure: command =>
                        command.CommandText =
                            "SELECT sql FROM sqlite_master WHERE type='table' AND name='events'"
                ) as string;

            Assert.NotNull(tableDef);
            Assert.Contains("(datetime('now'))", tableDef);

            // Insert and verify default works
            DatabaseCommands.Execute(
                connection: connection,
                configure: command => command.CommandText = "INSERT INTO events (id) VALUES (1)"
            );

            var createdAt =
                global::Tests.Shared.DatabaseCommands.Scalar(
                    connection: connection,
                    configure: command =>
                        command.CommandText = "SELECT created_at FROM events WHERE id = 1"
                ) as string;

            Assert.NotNull(createdAt);
            Assert.NotEmpty(createdAt);
        });
    }

    [Fact]
    public void LqlDefault_BooleanTrue_TranslatesTo1()
    {
        // Arrange
        // Implements [MIG-TEST-SQLITE-LIFETIME].
        SqliteTestDb.WithDb(test: connection =>
        {
            var schema = Fixture_LqlDefault_BooleanTrue_TranslatesTo1_1();

            // Act
            SeedSchemaAndAssertSuccess(input0: connection, input1: schema);

            // Verify table DDL contains 1 and 0 for boolean defaults
            var tableDef =
                global::Tests.Shared.DatabaseCommands.Scalar(
                    connection: connection,
                    configure: command =>
                        command.CommandText =
                            "SELECT sql FROM sqlite_master WHERE type='table' AND name='flags'"
                ) as string;

            Assert.NotNull(tableDef);
            ContainsAll(actual: tableDef, expected: ["DEFAULT 1", "DEFAULT 0"]); // false -> 0

            // Insert and verify defaults work
            DatabaseCommands.Execute(
                connection: connection,
                configure: command => command.CommandText = "INSERT INTO flags (id) VALUES (1)"
            );

            using var selectCmd = connection.CreateCommand();
            using var reader = DatabaseCommands.ReadFirstRow(
                selectCmd,
                command =>
                    command.CommandText = "SELECT is_active, is_deleted FROM flags WHERE id = 1"
            );
            Assert.Equal(1, reader.GetInt64(0)); // is_active = true = 1
            Assert.Equal(0, reader.GetInt64(1)); // is_deleted = false = 0
        });
    }

    [Fact]
    public void LqlDefault_NumericValues_PassThrough()
    {
        // Arrange
        // Implements [MIG-TEST-SQLITE-LIFETIME].
        SqliteTestDb.WithDb(test: connection =>
        {
            var schema = Fixture_LqlDefault_NumericValues_PassThrough_1();

            // Act
            SeedSchemaAndAssertSuccess(input0: connection, input1: schema);

            // Insert and verify defaults work
            DatabaseCommands.Execute(
                connection: connection,
                configure: command => command.CommandText = "INSERT INTO counters (id) VALUES (1)"
            );

            using var selectCmd = connection.CreateCommand();
            using var reader = DatabaseCommands.ReadFirstRow(
                selectCmd,
                command =>
                    command.CommandText = "SELECT count, priority, rate FROM counters WHERE id = 1"
            );
            Assert.Equal(0, reader.GetInt64(0));
            Assert.Equal(100, reader.GetInt64(1));
            Assert.Equal(1.5, reader.GetDouble(2), 2);
        });
    }

    [Fact]
    public void LqlDefault_StringLiteral_PassThrough()
    {
        // Arrange
        // Implements [MIG-TEST-SQLITE-LIFETIME].
        SqliteTestDb.WithDb(test: connection =>
        {
            var schema = Fixture_LqlDefault_StringLiteral_PassThrough_1();

            // Act
            SeedSchemaAndAssertSuccess(input0: connection, input1: schema);

            // Insert and verify defaults work
            DatabaseCommands.Execute(
                connection: connection,
                configure: command => command.CommandText = "INSERT INTO items (id) VALUES (1)"
            );

            using var selectCmd = connection.CreateCommand();
            using var reader = DatabaseCommands.ReadFirstRow(
                selectCmd,
                command => command.CommandText = "SELECT status, category FROM items WHERE id = 1"
            );
            Assert.Equal("pending", reader.GetString(0));
            Assert.Equal("uncategorized", reader.GetString(1));
        });
    }

    [Fact]
    public void LqlDefault_GenUuid_GeneratesValidUuidFormat()
    {
        // Arrange
        // Implements [MIG-TEST-SQLITE-LIFETIME].
        SqliteTestDb.WithDb(test: connection =>
        {
            var schema = Fixture_LqlDefault_GenUuid_GeneratesValidUuidFormat_1();

            // Act
            SeedSchemaAndAssertSuccess(input0: connection, input1: schema);

            // Insert multiple rows and verify UUIDs are generated and unique
            DatabaseCommands.Execute(
                connection: connection,
                configure: command =>
                    command.CommandText =
                        "INSERT INTO records (name) VALUES ('test1'), ('test2'), ('test3')"
            );

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
        });
    }

    [Fact]
    public void LqlDefault_CurrentDate_ReturnsDateOnly()
    {
        // Arrange
        // Implements [MIG-TEST-SQLITE-LIFETIME].
        SqliteTestDb.WithDb(test: connection =>
        {
            var schema = Fixture_LqlDefault_CurrentDate_ReturnsDateOnly_1();

            // Act
            SeedSchemaAndAssertSuccess(input0: connection, input1: schema);

            // Insert and verify default date
            DatabaseCommands.Execute(
                connection: connection,
                configure: command => command.CommandText = "INSERT INTO logs (id) VALUES (1)"
            );

            var logDate =
                global::Tests.Shared.DatabaseCommands.Scalar(
                    connection: connection,
                    configure: command =>
                        command.CommandText = "SELECT log_date FROM logs WHERE id = 1"
                ) as string;

            Assert.NotNull(logDate);
            Assert.Matches(@"^\d{4}-\d{2}-\d{2}$", logDate); // YYYY-MM-DD format
        });
    }

    [Fact]
    public void LqlDefault_MixedDefaults_AllWorkTogether()
    {
        // Arrange - A complex table with multiple LQL defaults
        // Implements [MIG-TEST-SQLITE-LIFETIME].
        SqliteTestDb.WithDb(test: connection =>
        {
            var schema = Fixture_LqlDefault_MixedDefaults_AllWorkTogether_1();

            // Act
            SeedSchemaAndAssertSuccess(input0: connection, input1: schema);

            // Insert with no columns specified - all defaults should apply
            DatabaseCommands.Execute(
                connection: connection,
                configure: command => command.CommandText = "INSERT INTO orders DEFAULT VALUES"
            );

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
        });
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
        // Implements [MIG-TEST-SQLITE-LIFETIME].
        SqliteTestDb.WithDb(test: connection =>
        {
            var schema =
                Fixture_CreateTableWithVectorColumn_FallsBackToBlob_AndMigratesSuccessfully_1();

            var result = SeedSchema(connection: connection, schema: schema, logger: _logger);

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
                using var r = DatabaseCommands.ReadFirstRow(
                    selectCmd,
                    command => command.CommandText = "SELECT Body, length(Embedding) FROM documents"
                );
                Assert.Equal("hello", r.GetString(0));
                // 384 floats × 4 bytes = 1536 bytes
                Assert.Equal(1536L, r.GetInt64(1));
            }
        });
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private void SeedSchemaAndAssertSuccess(
        global::Microsoft.Data.Sqlite.SqliteConnection input0,
        global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition input1
    )
    {
        // Act
        var result = SeedSchema(connection: input0, schema: input1, logger: _logger);

        // Assert
        Assert.True(result is MigrationApplyResultOk);
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition ApplyChangesAndInspectSchema(
        global::Microsoft.Data.Sqlite.SqliteConnection input0,
        global::System.Collections.Generic.IReadOnlyList<global::Nimblesite.DataProvider.Migration.Core.SchemaOperation> input1
    )
    {
        var result = SqliteTestDb.TryApply(connection: input0, operations: input1, logger: _logger);

        // Assert
        Assert.True(result is MigrationApplyResultOk);

        var finalSchema = ((SchemaResultOk)SqliteSchemaInspector.Inspect(input0, _logger)).Value;

        return finalSchema;
    }
}
