// Implements [CI-TEST-SHARED-COMMAND].

using static global::Tests.Shared.ResultAssertions;
using static global::Tests.Shared.StringAssertions;

namespace Nimblesite.DataProvider.Migration.Tests;

/// <summary>
/// E2E tests for PostgreSQL migrations using a shared Testcontainers postgres.
/// Each test gets its own database within the shared container.
/// </summary>
[Collection(PostgresTestSuite.Name)]
public sealed partial class PostgresMigrationTests(PostgresContainerFixture fixture)
    : PostgresDatabaseTest(fixture)
{
    protected override string DatabaseName => "migration_test";

    [Fact]
    public void CreateDatabaseFromScratch_SingleTable_Success()
    {
        // Arrange
        var schema = Fixture_CreateDatabaseFromScratch_SingleTable_Success_1();

        // Act
        // Act
        var operations = SchemaDiff.Calculate(
            (Ok(read: () => PostgresSchemaInspector.Inspect(Connection, "public", Logger))).Value,
            schema,
            logger: Logger
        );

        var ops = Ok(actual: operations).Value;

        var result = Apply(operations: ops);

        // Assert
        PostgresTestDb.AssertApplyResult(apply: result);

        // Verify table exists
        // Verify table exists
        var inspectedSchema = Ok(read: () =>
            PostgresSchemaInspector.Inspect(Connection, "public", Logger)
        ).Value;
        Assert.Contains(inspectedSchema.Tables, t => t.Name == "users");
    }

    [Fact]
    public void CreateDatabaseFromScratch_MultipleTablesWithForeignKeys_Success()
    {
        // Arrange
        VerifyReusableWorkflow7(
            input0: VerifySharedContinuation1,
            input1: Fixture_CreateDatabaseFromScratch_MultipleTablesWithForeignKeys_Success_1,
            input2: VerifySharedContinuation11
        );

        var inspected = InspectPublic();

        Assert.Contains(inspected.Tables, t => t.Name == "customers");
        Assert.Contains(inspected.Tables, t => t.Name == "invoices");

        var invoicesTable = inspected.Tables.First(t => t.Name == "invoices");
        Assert.NotEmpty(invoicesTable.ForeignKeys);
    }

    [Fact]
    public void UpgradeExistingDatabase_AddColumn_Success()
    {
        // Arrange - Create initial schema
        var upgradeOps = VerifyConsolidatedPhase2(
            input0: Fixture_UpgradeExistingDatabase_AddColumn_Success_1,
            input1: Fixture_UpgradeExistingDatabase_AddColumn_Success_2,
            input2: Logger
        );

        // Should have 2 AddColumn operations
        PostgresTestDb.AssertTwoAddColumns(upgradeOps: upgradeOps);

        global::Tests.Shared.ResultAssertions.Ok(actual: Apply(operations: upgradeOps));

        var finalSchema = InspectPublic();
        var products = finalSchema.Tables.Single(t => t.Name == "products");
        Assert.Equal(4, products.Columns.Count);
    }

    [Fact]
    public void UpgradeExistingDatabase_AddTable_Success()
    {
        // Arrange
        VerifyUpgradeOperation<CreateTableOperation>(
            input0: Fixture_UpgradeExistingDatabase_AddTable_Success_1,
            input2: Fixture_UpgradeExistingDatabase_AddTable_Success_2
        );

        var finalSchema = InspectPublic();
        Assert.Contains(finalSchema.Tables, t => t.Name == "items");
    }

    [Fact]
    public void UpgradeExistingDatabase_AddIndex_Success()
    {
        // Arrange
        VerifyUpgradeOperation<CreateIndexOperation>(
            input0: Fixture_UpgradeExistingDatabase_AddIndex_Success_1,
            input2: Fixture_UpgradeExistingDatabase_AddIndex_Success_2
        );

        var finalSchema = InspectPublic();
        var logs = finalSchema.Tables.Single(t => t.Name == "logs");
        Assert.Contains(logs.Indexes, i => i.Name == "idx_logs_level");
    }

    [Fact]
    public void Migration_IsIdempotent_NoErrorOnRerun()
    {
        // Arrange
        var schema = Fixture_Migration_IsIdempotent_NoErrorOnRerun_1();

        // Act - Run migration twice
        for (var i = 0; i < 2; i++)
        {
            var operations = InspectAndApplyInitialSchema(input0: schema);

            global::Tests.Shared.ResultAssertions.Ok(actual: Apply(operations: operations));

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
        CreateAndApplySchema(create: Fixture_CreateTable_PostgresNativeTypes_Success_1);

        // Verify native types are used
        using var cmd = Connection.CreateCommand();
        cmd.CommandText = Data_CreateTable_PostgresNativeTypes_Success_1;
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
        VerifyReusableWorkflow7(
            input0: VerifySharedContinuation1,
            input1: Fixture_CreateTable_MixedCaseColumnCheckConstraint_PreservesIdentifierCase_1,
            input2: VerifySharedContinuation11
        );
        InsertPatientGender("male");
        var ex = Assert.Throws<PostgresException>(() => InsertPatientGender("invalid"));
        Assert.Equal("23514", ex.SqlState);
    }

    [Fact]
    public void ExpressionIndex_CreateWithLowerFunction_Success()
    {
        // Arrange - Create table with expression index for case-insensitive uniqueness
        VerifyReusableWorkflow7(
            input0: VerifySharedContinuation1,
            input1: Fixture_ExpressionIndex_CreateWithLowerFunction_Success_1,
            input2: VerifySharedContinuation11
        );

        // Verify index exists and is unique
        using var cmd = Connection.CreateCommand();
        cmd.CommandText = Data_ExpressionIndex_CreateWithLowerFunction_Success_1;
        using var reader = cmd.ExecuteReader();

        Assert.True(reader.Read(), "Expression index should exist");
        var indexDef = reader.GetString(1);
        ContainsAll(actual: indexDef, expected: ["UNIQUE", "lower"]);
    }

    [Fact]
    public void ExpressionIndex_EnforcesCaseInsensitiveUniqueness()
    {
        // Arrange - Create table with expression index
        VerifySharedContinuation9(
            input0: Fixture_ExpressionIndex_EnforcesCaseInsensitiveUniqueness_1
        );

        // Act - Insert first venue
        DatabaseCommands.Execute(
            connection: Connection,
            configure: command =>
                command.CommandText = "INSERT INTO venues (name) VALUES ('The Corner Hotel')"
        );

        // Try to insert duplicate with different case - should fail
        using var duplicateCmd = Connection.CreateCommand();
        duplicateCmd.CommandText = "INSERT INTO venues (name) VALUES ('THE CORNER HOTEL')";

        // Assert - Should throw unique constraint violation
        var ex = Assert.Throws<PostgresException>(() => duplicateCmd.ExecuteNonQuery());
        Assert.Contains("uq_venues_name", ex.Message);
    }

    [Fact]
    public void ExpressionIndex_MultiExpression_CompositeIndexSuccess()
    {
        // Arrange - Create table with multi-expression index (like venues with suburb_id)
        VerifyReusableWorkflow7(
            input0: VerifySharedContinuation1,
            input1: Fixture_ExpressionIndex_MultiExpression_CompositeIndexSuccess_1,
            input2: VerifySharedContinuation11
        );

        // Verify composite expression index exists
        var indexDef = VerifySharedContinuation7(configure0: command =>
            command.CommandText = Data_ExpressionIndex_MultiExpression_CompositeIndexSuccess_1
        );
        ContainsAll(actual: indexDef, expected: ["UNIQUE", "lower", "suburb_id"]);
    }

    [Fact]
    public void ExpressionIndex_MultiExpression_AllowsSameNameDifferentSuburb()
    {
        // Arrange - Create tables with composite expression index
        VerifySharedContinuation9(
            input0: Fixture_ExpressionIndex_MultiExpression_AllowsSameNameDifferentSuburb_1
        );

        // Create two regions
        DatabaseCommands.Execute(
            connection: Connection,
            configure: command =>
                command.CommandText =
                    Data_ExpressionIndex_MultiExpression_AllowsSameNameDifferentSuburb_1
        );

        // Act - Insert same name in different regions (should succeed)
        using var insertCmd = Connection.CreateCommand();
        insertCmd.CommandText =
            Data_ExpressionIndex_MultiExpression_AllowsSameNameDifferentSuburb_2;
        var rowsAffected = insertCmd.ExecuteNonQuery();

        // Assert - Both inserts should succeed
        Assert.Equal(2, rowsAffected);

        // Try to insert duplicate in same region - should fail
        using var duplicateCmd = Connection.CreateCommand();
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
        var schema = Fixture_ExpressionIndex_Idempotent_NoErrorOnRerun_1();

        // Act - Run migration twice
        // Note: Expression indexes aren't read back by schema inspector (no expression index introspection)
        // but CREATE INDEX IF NOT EXISTS ensures idempotency at the database level
        for (var i = 0; i < 2; i++)
        {
            var currentSchema = InspectPublic();

            var result = CalculateAndApplySchemaChanges(input0: currentSchema, input1: schema);

            // Both runs should succeed - IF NOT EXISTS handles already-existing index
            PostgresTestDb.AssertApplyResult(apply: result);
        }

        // Verify expression index exists and is functional
        var indexDef = VerifySharedContinuation7(configure0: command =>
            command.CommandText = """
            SELECT indexdef FROM pg_indexes
            WHERE tablename = 'bands' AND indexname = 'uq_bands_name'
            """
        );
        Assert.Contains("lower", indexDef);
    }

    // =============================================================================
    // Index Conversion Tests (Column <-> Expression)
    // =============================================================================

    [Fact]
    public void UpgradeIndex_ColumnToExpression_RequiresDropAndCreate()
    {
        // Arrange - Create table with regular column index
        var indexDef = VerifyIndexConversion(
            input0: Fixture_UpgradeIndex_ColumnToExpression_RequiresDropAndCreate_1,
            input2: Fixture_UpgradeIndex_ColumnToExpression_RequiresDropAndCreate_2,
            query: """
            SELECT indexdef FROM pg_indexes
            WHERE tablename = 'artists' AND indexname = 'uq_artists_name_ci'
            """
        );
        Assert.Contains("lower", indexDef);
    }

    [Fact]
    public void UpgradeIndex_ExpressionToColumn_RequiresDropAndCreate()
    {
        // Arrange - Create table with expression index
        var indexDef = VerifyIndexConversion(
            input0: Fixture_UpgradeIndex_ExpressionToColumn_RequiresDropAndCreate_1,
            input2: Fixture_UpgradeIndex_ExpressionToColumn_RequiresDropAndCreate_2,
            query: """
            SELECT indexdef FROM pg_indexes
            WHERE tablename = 'venues' AND indexname = 'idx_venues_name'
            """
        );
        Assert.DoesNotContain("lower", indexDef);
    }

    [Fact]
    public void UpgradeIndex_SameNameDifferentType_NotDetectedWithoutDestructive()
    {
        // This test verifies that WITHOUT allowDestructive, the system doesn't
        // automatically detect that an index definition changed (column vs expression).
        // Converting an index type is a destructive operation requiring explicit opt-in.

        // Arrange - Create table with regular column index
        VerifySharedContinuation9(
            input0: Fixture_UpgradeIndex_SameNameDifferentType_NotDetectedWithoutDestructive_1
        );

        // v2 wants to change to expression index with SAME name
        // This is a semantic change - case-sensitive to case-insensitive
        var upgradeOps = VerifyReusableWorkflow27(
            input0: VerifySharedContinuation1,
            input1: Fixture_UpgradeIndex_SameNameDifferentType_NotDetectedWithoutDestructive_2,
            input2: Logger,
            value0: false
        );

        // Assert - No operations since index name already exists and we can't drop without destructive
        // The SchemaDiff only compares by name, not by definition
        Assert.Empty(upgradeOps);
    }

    [Fact]
    public void Destructive_DropTable_AllowedWithOption()
    {
        // Arrange - Create initial tables
        VerifySharedContinuation9(input0: Fixture_Destructive_DropTable_AllowedWithOption_1);

        // v2 removes dropme table
        var operations = VerifyReusableWorkflow27(
            input0: VerifySharedContinuation1,
            input1: Fixture_Destructive_DropTable_AllowedWithOption_2,
            input2: Logger,
            value0: true
        );

        Assert.Single(operations);
        Assert.IsType<DropTableOperation>(operations[0]);

        global::Tests.Shared.ResultAssertions.Ok(
            actual: Apply(operations: operations, options: MigrationOptions.Destructive)
        );

        var finalSchema = InspectPublic();
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
        VerifySharedContinuation3(
            configure0: command =>
                command.CommandText =
                    "INSERT INTO events (id) VALUES ('11111111-1111-1111-1111-111111111111')",
            input0: Fixture_LqlDefault_NowFunction_GeneratesCurrentTimestamp_1
        );

        // Verify the default was applied - should be a recent timestamp
        var createdAt = (DateTime)
            global::Tests.Shared.DatabaseCommands.Scalar(
                connection: Connection,
                configure: command =>
                    command.CommandText =
                        "SELECT created_at FROM events WHERE id = '11111111-1111-1111-1111-111111111111'"
            )!;

        // Should be within last few seconds
        Assert.True((DateTime.UtcNow - createdAt).TotalSeconds < 10);
    }

    [Fact]
    public void LqlDefault_GenUuidFunction_GeneratesValidUuid()
    {
        // Arrange - Create table with LQL gen_uuid() default
        VerifySharedContinuation3(
            configure0: command =>
                command.CommandText = Data_LqlDefault_GenUuidFunction_GeneratesValidUuid_1,
            input0: Fixture_LqlDefault_GenUuidFunction_GeneratesValidUuid_1
        );

        // Verify UUIDs were generated and are unique
        using var selectCmd = Connection.CreateCommand();
        selectCmd.CommandText = "SELECT id FROM items ORDER BY name";
        using var reader = selectCmd.ExecuteReader();

        var uuids =
            global::Nimblesite.DataProvider.Migration.Tests.PostgresRlsE2ETests.VerifyReusableWorkflow22(
                input0: reader
            );

        Assert.Equal(3, uuids.Count);
        Assert.Equal(3, uuids.Distinct().Count()); // All unique
        Assert.All(uuids, id => Assert.NotEqual(Guid.Empty, id));
    }

    // Implements [CI-TEST-SHARED-CASE-MATRIX]: boolean, numeric and string
    // literal defaults share one insert-then-read workflow. Case names preserve
    // the original fact labels; inputs and assertions are unchanged.
    [Theory]
    [MemberData(nameof(LqlDefaultFirstRowCases))]
    public void LqlDefault_FirstRow(
        string scenario,
        Func<SchemaDefinition> fixture,
        string insertSql,
        string selectSql,
        Action<System.Data.Common.DbDataReader> assert
    )
    {
        Assert.False(string.IsNullOrEmpty(scenario));
        VerifySharedContinuation3(
            configure0: command => command.CommandText = insertSql,
            input0: fixture
        );
        PostgresTestDb.WithFirstRow(Connection, selectSql, assert);
    }

    public static IEnumerable<object[]> LqlDefaultFirstRowCases()
    {
        Func<SchemaDefinition> booleanTrue = Fixture_LqlDefault_BooleanTrue_GeneratesCorrectValue_1;
        Action<System.Data.Common.DbDataReader> assertBooleanTrue = reader =>
        {
            Assert.True(reader.GetBoolean(0));
            Assert.False(reader.GetBoolean(1));
        };
        yield return
        [
            "LqlDefault_BooleanTrue_GeneratesCorrectValue",
            booleanTrue,
            "INSERT INTO flags (id) VALUES ('11111111-1111-1111-1111-111111111111')",
            "SELECT is_active, is_deleted FROM flags",
            assertBooleanTrue,
        ];

        Func<SchemaDefinition> numeric =
            Fixture_LqlDefault_NumericLiterals_GeneratesCorrectValues_1;
        Action<System.Data.Common.DbDataReader> assertNumeric = reader =>
        {
            Assert.Equal(0, reader.GetInt32(0));
            Assert.Equal(100m, reader.GetDecimal(1));
            Assert.Equal(0.5, reader.GetDouble(2), 2);
        };
        yield return
        [
            "LqlDefault_NumericLiterals_GeneratesCorrectValues",
            numeric,
            "INSERT INTO counters (id) VALUES ('11111111-1111-1111-1111-111111111111')",
            "SELECT count, score, rate FROM counters",
            assertNumeric,
        ];

        Func<SchemaDefinition> stringLiteral =
            Fixture_LqlDefault_StringLiteral_GeneratesCorrectValue_1;
        Action<System.Data.Common.DbDataReader> assertStringLiteral = reader =>
        {
            Assert.Equal("pending", reader.GetString(0));
            Assert.Equal("default", reader.GetString(1));
        };
        yield return
        [
            "LqlDefault_StringLiteral_GeneratesCorrectValue",
            stringLiteral,
            "INSERT INTO statuses (id) VALUES ('11111111-1111-1111-1111-111111111111')",
            "SELECT status, category FROM statuses",
            assertStringLiteral,
        ];
    }

    [Fact]
    public void LqlDefault_AllTypesInOneTable_WorksTogether()
    {
        // Arrange - Comprehensive test with all LQL default types
        VerifySharedContinuation3(
            configure0: command =>
                command.CommandText =
                    "INSERT INTO comprehensive_defaults (name) VALUES ('Test Item')",
            input0: Fixture_LqlDefault_AllTypesInOneTable_WorksTogether_1
        );

        // Verify all defaults
        using var selectCmd = Connection.CreateCommand();
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
    // a real database, including vector columns used by external consumers.
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void CreateTableWithVectorColumn_MigratesAgainstPgvectorContainer_Success()
    {
        // Arrange — a Documents table with a 384-dim embedding, the
        // sentence-transformers default.
        var operations = VerifySharedPhase3(
            input0: Fixture_CreateTableWithVectorColumn_MigratesAgainstPgvectorContainer_Success_1,
            input1: Logger
        );

        // Act
        var result = Apply(operations: operations);

        // Assert — migration succeeded
        PostgresTestDb.AssertApplyResult(apply: result);

        // Assert — documents table exists
        var inspected = InspectPublic();
        Assert.Contains(inspected.Tables, t => t.Name == "documents");

        // Assert — pgvector extension is installed in the database
        using (var extCmd = Connection.CreateCommand())
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
        using (var typeCmd = Connection.CreateCommand())
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
        using (var insertCmd = Connection.CreateCommand())
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

        using (var selectCmd = Connection.CreateCommand())
        {
            using var reader = DatabaseCommands.ReadFirstRow(
                selectCmd,
                command =>
                    command.CommandText =
                        "SELECT body, vector_dims(embedding) FROM public.documents"
            );
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
        var schema = Fixture_CreateTableWithVectorColumn_OpenAiLargeDim_Success_1();

        var operations = InspectAndApplyInitialSchema(input0: schema);

        // Act
        var result = Apply(operations: operations);

        // Assert
        PostgresTestDb.AssertApplyResult(apply: result);

        Assert.Equal(
            "vector(3072)",
            (string?)
                global::Tests.Shared.DatabaseCommands.Scalar(
                    connection: Connection,
                    configure: command =>
                        command.CommandText =
                            "SELECT format_type(a.atttypid, a.atttypmod) "
                            + "FROM pg_attribute a "
                            + "JOIN pg_class c ON c.oid = a.attrelid "
                            + "WHERE c.relname = 'large_embeddings' AND a.attname = 'embedding'"
                )
        );
    }

    private void InsertPatientGender(string gender)
    {
        using var command = Connection.CreateCommand();
        command.CommandText =
            "INSERT INTO \"fhir_patient\" (\"id\", \"Gender\") VALUES (@id, @gender)";
        command.Parameters.AddWithValue("@id", Guid.NewGuid());
        command.Parameters.AddWithValue("@gender", gender);
        command.ExecuteNonQuery();
    }

    // Implements [MIG-TEST-POSTGRES-RUNNER].
    private MigrationApplyResult Apply(
        IReadOnlyList<SchemaOperation> operations,
        MigrationOptions? options = null
    ) =>
        PostgresTestDb.TryApply(
            connection: Connection,
            operations: operations,
            logger: Logger,
            options: options
        );

    private SchemaDefinition InspectPublic() => PostgresTestDb.Inspect(Connection, Logger);

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private global::Outcome.Result<
        global::System.Boolean,
        global::Nimblesite.DataProvider.Migration.Core.MigrationError
    > CalculateAndApplySchemaChanges(
        global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition input0,
        global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition input1
    )
    {
        var operations = SchemaDiffAssertions.Diff(
            current: input0,
            desired: input1,
            logger: Logger
        );

        var result = Apply(operations: operations);

        return result;
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private global::System.Collections.Generic.IReadOnlyList<global::Nimblesite.DataProvider.Migration.Core.SchemaOperation> InspectAndApplyInitialSchema(
        global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition input0
    )
    {
        var emptySchema = InspectPublic();
        var v1Ops = SchemaDiffAssertions.Diff(
            current: emptySchema,
            desired: input0,
            logger: Logger
        );

        return v1Ops;
    }

    // Implements [MIG-TEST-PG-SCHEMA-WORKFLOW].
    private SchemaDefinition CreateAndApplySchema(Func<SchemaDefinition> create)
    {
        var schema = create();
        global::Tests.Shared.ResultAssertions.Ok(
            actual: CalculateAndApplySchemaChanges(input0: InspectPublic(), input1: schema)
        );
        return schema;
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private (SchemaDefinition, SchemaDefinition) VerifySharedContinuation1(
        Func<SchemaDefinition> input0
    )
    { // Arrange
        var schema = input0();

        // Act
        var emptySchema = InspectPublic();

        return (schema, emptySchema);
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private void VerifySharedContinuation3(
        Func<SchemaDefinition> input0,
        global::System.Action<global::System.Data.Common.DbCommand> configure0
    )
    { // Arrange - Create table with LQL now() default
        CreateAndApplySchema(create: input0);

        // Insert a row without specifying created_at - should use default
        DatabaseCommands.Execute(connection: Connection, configure: configure0);
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private string VerifySharedContinuation7(
        global::System.Action<global::System.Data.Common.DbCommand> configure0
    )
    {
        // Verify composite expression index exists
        var indexDef = Assert.IsType<string>(
            global::Tests.Shared.DatabaseCommands.Scalar(
                connection: Connection,
                configure: configure0
            )
        );

        Assert.NotNull(indexDef);

        return indexDef;
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private void VerifySharedContinuation9(Func<SchemaDefinition> input0)
    { // Arrange - Create initial schema
        var v1 = input0();

        var v1Ops = InspectAndApplyInitialSchema(input0: v1);
        _ = Apply(operations: v1Ops);
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private void VerifySharedContinuation11(SchemaDefinition input0, SchemaDefinition input1)
    {
        var result = CalculateAndApplySchemaChanges(input0: input0, input1: input1);

        // Assert
        PostgresTestDb.AssertApplyResult(apply: result);
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private void VerifySharedContinuation14(SchemaDefinition input0, SchemaDefinition input1)
    {
        var upgradeOps = (
            global::Tests.Shared.ResultAssertions.Ok(
                actual: SchemaDiff.Calculate(input0, input1, allowDestructive: true, logger: Logger)
            )
        ).Value;

        // Assert - Should have drop old index + create new expression index
        SchemaDiffAssertions.AssertDropAndCreateIndex(upgradeOps);

        // Apply the upgrade
        var result = Apply(operations: upgradeOps, options: MigrationOptions.Destructive);

        PostgresTestDb.AssertApplyResult(apply: result);
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    internal IReadOnlyList<SchemaOperation> VerifySharedPhase3(
        Func<SchemaDefinition> input0,
        ILogger input1
    )
    {
        // Define v2 with new columns
        var (v2, currentSchema) = VerifySharedContinuation1(input0: input0);

        var upgradeOps = (
            global::Tests.Shared.ResultAssertions.Ok(
                actual: SchemaDiff.Calculate(currentSchema, v2, logger: input1)
            )
        ).Value;

        return upgradeOps;
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private void VerifyUpgradeOperation<T>(
        Func<SchemaDefinition> input0,
        Func<SchemaDefinition> input2
    )
        where T : SchemaOperation
    {
        var upgradeOps = VerifyConsolidatedPhase2(input0: input0, input1: input2, input2: Logger);
        Assert.Single(upgradeOps);
        Assert.IsType<T>(upgradeOps[0]);
        global::Tests.Shared.ResultAssertions.Ok(actual: Apply(operations: upgradeOps));
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private string VerifyIndexConversion(
        Func<SchemaDefinition> input0,
        Func<SchemaDefinition> input2,
        string query
    )
    {
        VerifyConsolidatedPhase4(input0: input0, input1: Logger, input2: input2);
        return VerifySharedContinuation7(configure0: command => command.CommandText = query);
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    internal IReadOnlyList<SchemaOperation> VerifyConsolidatedPhase2(
        Func<SchemaDefinition> input0,
        Func<SchemaDefinition> input1,
        ILogger input2
    )
    { // Arrange - Create initial schema
        VerifySharedContinuation9(input0: input0);

        // Define v2 with new columns
        var upgradeOps = VerifySharedPhase3(input0: input1, input1: input2);

        return upgradeOps;
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    internal void VerifyConsolidatedPhase4(
        Func<SchemaDefinition> input0,
        ILogger input1,
        Func<SchemaDefinition> input2
    )
    { // Arrange - Create table with regular column index
        var v1Ops = VerifySharedPhase3(input0: input0, input1: input1);
        _ = Apply(operations: v1Ops);

        // v2 changes to expression index (different name since semantically different)
        VerifyReusableWorkflow7(
            input0: VerifySharedContinuation1,
            input1: input2,
            input2: VerifySharedContinuation14
        );
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    // Implements [CI-TEST-SHARED-SCENARIOS].
    private static void VerifyReusableWorkflow7(
        Func<Func<SchemaDefinition>, (SchemaDefinition, SchemaDefinition)> input0,
        Func<SchemaDefinition> input1,
        Action<SchemaDefinition, SchemaDefinition> input2
    )
    { // Arrange
        var (schema, emptySchema) = input0(arg: input1);
        input2(arg1: emptySchema, arg2: schema);
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    // Implements [CI-TEST-SHARED-SCENARIOS].
    private static IReadOnlyList<SchemaOperation> VerifyReusableWorkflow27(
        Func<Func<SchemaDefinition>, (SchemaDefinition, SchemaDefinition)> input0,
        Func<SchemaDefinition> input1,
        ILogger input2,
        bool value0
    )
    {
        // v2 wants to change to expression index with SAME name
        // This is a semantic change - case-sensitive to case-insensitive
        var (v2, currentSchema) = input0(arg: input1);
        var upgradeOps = (
            global::Tests.Shared.ResultAssertions.Ok(
                actual: SchemaDiff.Calculate(
                    currentSchema,
                    v2,
                    allowDestructive: value0,
                    logger: input2
                )
            )
        ).Value;
        return upgradeOps;
    }
}
