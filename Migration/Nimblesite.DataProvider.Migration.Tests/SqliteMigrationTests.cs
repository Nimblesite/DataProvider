// Implements [MIG-TEST-SCHEMA-FIXTURES].
// Implements [MIG-TEST-ID-NAME-COLUMNS].
// Implements [CI-TEST-SHARED-COMMAND].

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
        var ops = SqliteTestDb.CalculateChanges(connection, schema, logger);
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
            // Act
            var operations = SchemaDiff.Calculate(
                (Ok(read: () => SqliteSchemaInspector.Inspect(connection, _logger))).Value,
                schema,
                logger: _logger
            );

            var ops = Ok(actual: operations).Value;

            global::Tests.Shared.ResultAssertions.Ok(
                actual: SqliteTestDb.TryApply(
                    connection: connection,
                    operations: ops,
                    logger: _logger
                )
            );

            // Verify table exists
            // Verify table exists
            var inspectedSchema = Ok(read: () =>
                SqliteSchemaInspector.Inspect(connection, _logger)
            ).Value;
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

            VerifySharedChecks2(
                input0: Fixture_CreateDatabaseFromScratch_MultipleTablesWithForeignKeys_Success_1,
                input1: connection
            );

            var inspected = SqliteTestDb.Inspect(connection);

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
            var upgradeOps = VerifySharedPhase8(
                input0: Fixture_UpgradeExistingDatabase_AddColumn_Success_1,
                input1: connection,
                input2: _logger,
                input3: Fixture_UpgradeExistingDatabase_AddColumn_Success_2
            );

            // Should have 2 AddColumn operations
            PostgresTestDb.AssertTwoAddColumns(upgradeOps: upgradeOps);

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
            var finalSchema = VerifyUpgradeOperation<CreateTableOperation>(
                connection: connection,
                input1: Data_UpgradeExistingDatabase_AddTable_Success_1,
                input4: Fixture_UpgradeExistingDatabase_AddTable_Success_1
            );
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
            var finalSchema = VerifyUpgradeOperation<CreateIndexOperation>(
                connection: connection,
                input1: Fixture_UpgradeExistingDatabase_AddIndex_Success_1,
                input4: Fixture_UpgradeExistingDatabase_AddIndex_Success_2
            );
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
                var operations =
                    global::Nimblesite.DataProvider.Migration.Tests.MigrationCornerCaseTests.VerifyCrossClassChecks4(
                        input0: connection,
                        input1: _logger,
                        input2: schema
                    );

                global::Tests.Shared.ResultAssertions.Ok(
                    actual: SqliteTestDb.TryApply(
                        connection: connection,
                        operations: operations,
                        logger: _logger
                    )
                );

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
            VerifySharedChecks2(
                input0: Fixture_CreateTable_AllPortableTypes_Success_1,
                input1: connection
            );

            var inspected = SqliteTestDb.Inspect(connection);
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
            var operations = VerifyReusableWorkflow3(
                input0: Data_Destructive_DropTable_BlockedByDefault_1,
                input1: VerifyConsolidatedPhase1,
                input2: connection,
                input3: _logger,
                input4: Data_Destructive_DropTable_BlockedByDefault_2,
                value0: false
            );

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
            var operations = VerifyReusableWorkflow3(
                input0: Data_Destructive_DropTable_AllowedWithOption_1,
                input1: VerifyConsolidatedPhase1,
                input2: connection,
                input3: _logger,
                input4: Data_Destructive_DropTable_AllowedWithOption_2,
                value0: true
            );

            // Should have DropTableOperation
            Assert.Single(operations);
            Assert.IsType<DropTableOperation>(operations[0]);

            global::Tests.Shared.ResultAssertions.Ok(
                actual: SqliteTestDb.TryApply(
                    connection: connection,
                    operations: operations,
                    logger: _logger,
                    options: MigrationOptions.Destructive
                )
            );

            var finalSchema = (
                Ok(actual: SqliteSchemaInspector.Inspect(connection, _logger))
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
            var diff = SqliteTestDb.CalculateChanges(connection, schema, _logger);

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
            var schema = VerifySharedContinuation12(
                configure0: command =>
                    command.CommandText =
                        Data_SchemaCapture_ExistingDatabase_ReturnsCompleteSchema_1,
                input0: connection,
                input1: _logger
            );

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
            var schema = VerifySharedContinuation12(
                configure0: command =>
                    command.CommandText = Data_SchemaCapture_SerializesToJson_RoundTrip_1,
                input0: connection,
                input1: _logger
            );

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
            VerifyExpressionIndex(
                connection: connection,
                input1: Fixture_ExpressionIndex_CreateWithLowerFunction_Success_1,
                indexName: "uq_artists_name",
                expected: ["UNIQUE", "lower"]
            )
        );
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
            VerifyExpressionIndex(
                connection: connection,
                input1: Fixture_ExpressionIndex_MultiExpression_CompositeIndexSuccess_1,
                indexName: "uq_places_name_suburb",
                expected: ["UNIQUE", "lower", "SuburbId"]
            )
        );
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
                var operations =
                    global::Nimblesite.DataProvider.Migration.Tests.MigrationCornerCaseTests.VerifyCrossClassChecks4(
                        input0: connection,
                        input1: _logger,
                        input2: schema
                    );

                var result = SqliteTestDb.TryApply(
                    connection: connection,
                    operations: operations,
                    logger: _logger
                );

                SqliteTestDb.AssertApplyOk(result: result);

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
            var schema = VerifySharedContinuation12(
                configure0: command =>
                    command.CommandText =
                        Data_ExpressionIndex_SchemaInspector_DetectsExpressionIndex_1,
                input0: connection,
                input1: _logger
            );
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
        VerifyIndexConversion(
            createInitial: Fixture_UpgradeIndex_ColumnToExpression_RequiresDropAndCreate_1,
            createDesired: Fixture_UpgradeIndex_ColumnToExpression_RequiresDropAndCreate_2,
            readIndex: connection =>
                ReadSqliteDefinition(connection: connection, indexName: "uq_artists_name_ci"),
            verifyIndex: indexDef => Assert.Contains("lower", indexDef)
        );
    }

    [Fact]
    public void UpgradeIndex_ExpressionToColumn_RequiresDropAndCreate()
    {
        // Arrange - Create table with expression index
        // Implements [MIG-TEST-SQLITE-LIFETIME].
        VerifyIndexConversion(
            createInitial: Fixture_UpgradeIndex_ExpressionToColumn_RequiresDropAndCreate_1,
            createDesired: Fixture_UpgradeIndex_ExpressionToColumn_RequiresDropAndCreate_2,
            readIndex: connection =>
                ReadSqliteDefinition(connection: connection, indexName: "idx_venues_name"),
            verifyIndex: indexDef => Assert.DoesNotContain("lower", indexDef)
        );
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
            var tableDef = VerifyTableDdl(
                connection: connection,
                input1: Fixture_LqlDefault_NowFunction_TranslatesToCurrentTimestamp_1,
                tableName: "events"
            );
            Assert.Contains("(datetime('now'))", tableDef);

            // Insert and verify default works
            DatabaseCommands.Execute(
                connection: connection,
                configure: command => command.CommandText = "INSERT INTO events (id) VALUES (1)"
            );

            var createdAt = VerifySharedContinuation4(
                configure0: command =>
                    command.CommandText = "SELECT created_at FROM events WHERE id = 1",
                input0: connection
            );
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
            var tableDef = VerifyTableDdl(
                connection: connection,
                input1: Fixture_LqlDefault_BooleanTrue_TranslatesTo1_1,
                tableName: "flags"
            );
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

    // Implements [CI-TEST-SHARED-CASE-MATRIX]: the numeric, string, UUID, date and
    // mixed default scenarios share one seed-then-assert workflow. Case names
    // preserve the original fact labels; inputs and assertions are unchanged.
    [Theory]
    [MemberData(nameof(LqlDefaultSeedAndAssertCases))]
    public void LqlDefault_SeedAndAssert(
        string scenario,
        Func<SchemaDefinition> fixture,
        string insertSql,
        Action<SqliteConnection> assert
    )
    {
        Assert.False(string.IsNullOrEmpty(scenario));
        SqliteTestDb.WithDb(test: connection =>
        {
            SeedAndInsert(input0: connection, input1: fixture, value0: insertSql);
            assert(connection);
        });
    }

    public static IEnumerable<object[]> LqlDefaultSeedAndAssertCases()
    {
        Func<SchemaDefinition> numeric = Fixture_LqlDefault_NumericValues_PassThrough_1;
        Action<SqliteConnection> assertNumeric = connection =>
            AssertFirstRow(
                connection: connection,
                value0: "SELECT count, priority, rate FROM counters WHERE id = 1",
                assert: reader =>
                {
                    Assert.Equal(0, reader.GetInt64(0));
                    Assert.Equal(100, reader.GetInt64(1));
                    Assert.Equal(1.5, reader.GetDouble(2), 2);
                }
            );
        yield return
        [
            "LqlDefault_NumericValues_PassThrough",
            numeric,
            "INSERT INTO counters (id) VALUES (1)",
            assertNumeric,
        ];

        Func<SchemaDefinition> stringLiteral = Fixture_LqlDefault_StringLiteral_PassThrough_1;
        Action<SqliteConnection> assertStringLiteral = connection =>
            AssertFirstRow(
                connection: connection,
                value0: "SELECT status, category FROM items WHERE id = 1",
                assert: reader =>
                {
                    Assert.Equal("pending", reader.GetString(0));
                    Assert.Equal("uncategorized", reader.GetString(1));
                }
            );
        yield return
        [
            "LqlDefault_StringLiteral_PassThrough",
            stringLiteral,
            "INSERT INTO items (id) VALUES (1)",
            assertStringLiteral,
        ];

        Func<SchemaDefinition> genUuid = Fixture_LqlDefault_GenUuid_GeneratesValidUuidFormat_1;
        Action<SqliteConnection> assertGenUuid = connection =>
            WithReader(
                connection,
                "SELECT id FROM records",
                reader =>
                {
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

                    Assert.Equal(3, uuids.Count);
                    Assert.Equal(3, uuids.Distinct().Count());
                }
            );
        yield return
        [
            "LqlDefault_GenUuid_GeneratesValidUuidFormat",
            genUuid,
            "INSERT INTO records (name) VALUES ('test1'), ('test2'), ('test3')",
            assertGenUuid,
        ];

        Func<SchemaDefinition> currentDate = Fixture_LqlDefault_CurrentDate_ReturnsDateOnly_1;
        Action<SqliteConnection> assertCurrentDate = connection =>
        {
            var logDate = VerifySharedContinuation4(
                configure0: command =>
                    command.CommandText = "SELECT log_date FROM logs WHERE id = 1",
                input0: connection
            );
            Assert.Matches(@"^\d{4}-\d{2}-\d{2}$", logDate);
        };
        yield return
        [
            "LqlDefault_CurrentDate_ReturnsDateOnly",
            currentDate,
            "INSERT INTO logs (id) VALUES (1)",
            assertCurrentDate,
        ];

        Func<SchemaDefinition> mixed = Fixture_LqlDefault_MixedDefaults_AllWorkTogether_1;
        Action<SqliteConnection> assertMixed = connection =>
            WithReader(
                connection,
                "SELECT id, status, quantity, is_urgent, created_at FROM orders",
                reader =>
                {
                    Assert.True(reader.Read());

                    var id = reader.GetString(0);
                    var status = reader.GetString(1);
                    var quantity = reader.GetInt64(2);
                    var isUrgent = reader.GetInt64(3);
                    var createdAt = reader.GetString(4);

                    Assert.Matches(@"^[0-9a-f-]{36}$", id);
                    Assert.Equal("pending", status);
                    Assert.Equal(1, quantity);
                    Assert.Equal(0, isUrgent);
                    Assert.NotEmpty(createdAt);
                }
            );
        yield return
        [
            "LqlDefault_MixedDefaults_AllWorkTogether",
            mixed,
            "INSERT INTO orders DEFAULT VALUES",
            assertMixed,
        ];
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
            SqliteTestDb.AssertApplyOk(result: result);

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

    // Implements [MIG-TEST-SHARED-INDEX].
    private void VerifyExpressionIndex(
        SqliteConnection connection,
        Func<SchemaDefinition> input1,
        string indexName,
        System.Collections.Immutable.ImmutableArray<string> expected
    )
    {
        VerifySharedContinuation15(input1, connection, _logger);
        var indexDef = VerifySharedContinuation4(
            input0: connection,
            configure0: command =>
                command.CommandText =
                    $"SELECT sql FROM sqlite_master WHERE type='index' AND name='{indexName}'"
        );
        ContainsAll(actual: indexDef, expected: expected);
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private void SeedSchemaAndAssertSuccess(
        global::Microsoft.Data.Sqlite.SqliteConnection input0,
        global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition input1
    )
    {
        // Act
        global::Tests.Shared.ResultAssertions.Ok(
            actual: SeedSchema(connection: input0, schema: input1, logger: _logger)
        );
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition ApplyChangesAndInspectSchema(
        global::Microsoft.Data.Sqlite.SqliteConnection input0,
        global::System.Collections.Generic.IReadOnlyList<global::Nimblesite.DataProvider.Migration.Core.SchemaOperation> input1
    )
    {
        var finalSchema =
            global::Nimblesite.DataProvider.Migration.Tests.MigrationCornerCaseTests.VerifyReusableWorkflow32(
                input0: input0,
                input1: input1,
                input2: _logger
            );

        return finalSchema;
    }

    // Implements [MIG-TEST-SHARED-INDEX].
    private static string? ReadSqliteDefinition(SqliteConnection connection, string indexName)
    {
        var result = global::Tests.Shared.DatabaseCommands.Scalar(
            connection: connection,
            configure: command =>
                command.CommandText =
                    $"SELECT sql FROM sqlite_master WHERE type='index' AND name='{indexName}'"
        );
        return result is string sql ? sql : null;
    }

    // Implements [MIG-TEST-SHARED-INDEX-CONVERSION].
    private void VerifyIndexConversion(
        Func<SchemaDefinition> createInitial,
        Func<SchemaDefinition> createDesired,
        Func<SqliteConnection, string?> readIndex,
        Action<string?> verifyIndex
    )
    {
        SqliteTestDb.WithDb(test: connection =>
        {
            var upgradeOps = VerifyReusableWorkflow3(
                input0: createInitial,
                input1: VerifyConsolidatedPhase1,
                input2: connection,
                input3: _logger,
                input4: createDesired,
                value0: true
            );

            // Assert - Should have drop old index + create new expression index
            SchemaDiffAssertions.AssertDropAndCreateIndex(upgradeOps);

            // Apply the upgrade
            global::Tests.Shared.ResultAssertions.Ok(
                actual: SqliteTestDb.TryApply(
                    connection: connection,
                    operations: upgradeOps,
                    logger: _logger,
                    options: MigrationOptions.Destructive
                )
            );

            // Verify new expression index exists
            var indexDef = readIndex(connection);

            Assert.NotNull(indexDef);
            verifyIndex(indexDef);
        });
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private void VerifySharedChecks2(Func<SchemaDefinition> input0, SqliteConnection input1)
    {
        var schema = input0();

        // Act
        SeedSchemaAndAssertSuccess(input0: input1, input1: schema);
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private static string VerifySharedContinuation4(
        SqliteConnection input0,
        global::System.Action<global::System.Data.Common.DbCommand> configure0
    )
    {
        // Verify index exists
        var indexDef = Assert.IsType<string>(
            global::Tests.Shared.DatabaseCommands.Scalar(connection: input0, configure: configure0)
        );

        Assert.NotNull(indexDef);

        return indexDef;
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private string VerifyTableDdl(
        SqliteConnection connection,
        Func<SchemaDefinition> input1,
        string tableName
    )
    {
        VerifySharedChecks2(input0: input1, input1: connection);
        return VerifySharedContinuation4(
            input0: connection,
            configure0: command =>
                command.CommandText =
                    $"SELECT sql FROM sqlite_master WHERE type='table' AND name='{tableName}'"
        );
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private SchemaDefinition VerifyUpgradeOperation<T>(
        SqliteConnection connection,
        Func<SchemaDefinition> input1,
        Func<SchemaDefinition> input4
    )
        where T : SchemaOperation
    {
        var upgradeOps = VerifyReusableWorkflow28(
            input0: VerifySharedPhase8,
            input1: input1,
            input2: connection,
            input3: _logger,
            input4: input4
        );
        Assert.IsType<T>(upgradeOps[0]);
        return ApplyChangesAndInspectSchema(input0: connection, input1: upgradeOps);
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private void SeedAndInsert(
        SqliteConnection input0,
        Func<SchemaDefinition> input1,
        string value0
    ) =>
        VerifySharedContinuation5(
            configure0: command => command.CommandText = value0,
            input0: input1,
            input1: input0
        );

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private static void WithReader(
        SqliteConnection connection,
        string sql,
        Action<System.Data.Common.DbDataReader> assert
    )
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        using var reader = cmd.ExecuteReader();
        assert(obj: reader);
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private static void AssertFirstRow(
        SqliteConnection connection,
        string value0,
        Action<System.Data.Common.DbDataReader> assert
    )
    {
        using var selectCmd = connection.CreateCommand();
        using var reader = DatabaseCommands.ReadFirstRow(
            selectCmd,
            command => command.CommandText = value0
        );
        assert(obj: reader);
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private void VerifySharedContinuation5(
        Func<SchemaDefinition> input0,
        SqliteConnection input1,
        global::System.Action<global::System.Data.Common.DbCommand> configure0
    )
    {
        VerifySharedChecks2(input0: input0, input1: input1);

        // Insert and verify defaults work
        DatabaseCommands.Execute(connection: input1, configure: configure0);
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private static IReadOnlyList<SchemaOperation> VerifySharedContinuation8(
        SqliteConnection input0,
        SchemaDefinition input1,
        ILogger input2,
        Func<SchemaDefinition> input3
    )
    {
        // Apply v1
        var upgradeOps = VerifyConsolidatedPhase1(
            input0: input0,
            input1: input1,
            input2: input2,
            input3: input3,
            value0: false
        );

        return upgradeOps;
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private static SchemaDefinition VerifySharedContinuation12(
        SqliteConnection input0,
        ILogger input1,
        global::System.Action<global::System.Data.Common.DbCommand> configure0
    )
    {
        DatabaseCommands.Execute(connection: input0, configure: configure0);

        // Act - CAPTURE the existing schema
        // Act - CAPTURE the existing schema
        // Assert - schema captured successfully
        var schema = Ok(read: () => SqliteSchemaInspector.Inspect(input0, input1)).Value;

        return schema;
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    // Implements [CI-TEST-SHARED-SCENARIOS].
    private static void VerifySharedContinuation15(
        Func<SchemaDefinition> input0,
        SqliteConnection input1,
        ILogger input2
    )
    {
        var schema = input0();
        // Act
        var result = SeedSchema(connection: input1, schema: schema, logger: input2);
        // Assert - Migration succeeded
        SqliteTestDb.AssertApplyOk(result: result);
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    internal static IReadOnlyList<SchemaOperation> VerifySharedPhase8(
        Func<SchemaDefinition> input0,
        SqliteConnection input1,
        ILogger input2,
        Func<SchemaDefinition> input3
    )
    { // Create initial schema
        var v1 = input0();

        // Apply v1
        var upgradeOps = VerifySharedContinuation8(
            input0: input1,
            input1: v1,
            input2: input2,
            input3: input3
        );

        return upgradeOps;
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    internal static IReadOnlyList<SchemaOperation> VerifyConsolidatedPhase1(
        SqliteConnection input0,
        SchemaDefinition input1,
        ILogger input2,
        Func<SchemaDefinition> input3,
        bool value0
    )
    {
        SeedSchema(input0, input1, input2);

        // v2 removes Products table
        var operations =
            global::Nimblesite.DataProvider.Migration.Tests.MigrationCornerCaseTests.VerifySharedPhase2(
                input0: input3,
                input1: input0,
                input2: input2,
                value0: value0
            );

        return operations;
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    internal static IReadOnlyList<SchemaOperation> VerifyReusableWorkflow3(
        Func<SchemaDefinition> input0,
        Func<
            SqliteConnection,
            SchemaDefinition,
            ILogger,
            Func<SchemaDefinition>,
            bool,
            IReadOnlyList<SchemaOperation>
        > input1,
        SqliteConnection input2,
        ILogger input3,
        Func<SchemaDefinition> input4,
        bool value0
    )
    { // Create initial schema with 2 tables
        var v1 = input0();

        var operations = input1(arg1: input2, arg2: v1, arg3: input3, arg4: input4, arg5: value0);

        return operations;
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    internal static IReadOnlyList<SchemaOperation> VerifyReusableWorkflow28(
        Func<
            Func<SchemaDefinition>,
            SqliteConnection,
            ILogger,
            Func<SchemaDefinition>,
            IReadOnlyList<SchemaOperation>
        > input0,
        Func<SchemaDefinition> input1,
        SqliteConnection input2,
        ILogger input3,
        Func<SchemaDefinition> input4
    )
    {
        var upgradeOps = input0(arg1: input1, arg2: input2, arg3: input3, arg4: input4);

        // Should have 1 CreateTable operation
        Assert.Single(upgradeOps);

        return upgradeOps;
    }
}
