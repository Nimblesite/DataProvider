using static global::Tests.Shared.ResultAssertions;

// Implements [MIG-TEST-ID-NAME-COLUMNS].
namespace Nimblesite.DataProvider.Migration.Tests;

/// <summary>
/// Corner case and edge case tests for migrations.
/// Tests special characters, reserved words, extreme values, and unusual schemas.
/// </summary>
public sealed partial class MigrationCornerCaseTests
{
    private readonly ILogger _logger = NullLogger.Instance;

    private static (SqliteConnection Connection, string DbPath) CreateTestDb()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"cornercases_{Guid.NewGuid()}.db");
        var connection = new SqliteConnection($"Data Source={dbPath}");
        connection.Open();
        return (connection, dbPath);
    }

    private static void CleanupTestDb(SqliteConnection connection, string dbPath)
    {
        connection.Close();
        connection.Dispose();
        global::Tests.Shared.TempSqliteDatabase.DeleteIfExists(path: dbPath);
    }

    #region Special Characters and Reserved Words

    [Fact]
    public void TableName_WithUnderscores_Success()
    {
        var (connection, dbPath) = CreateTestDb();
        try
        {
            VerifySharedChecks4(
                input0: Fixture_TableName_WithUnderscores_Success_1,
                input1: connection
            );
            VerifyTableExists(connection, "user_roles_history");
        }
        finally
        {
            CleanupTestDb(connection, dbPath);
        }
    }

    [Fact]
    public void ColumnName_IsReservedWord_HandledCorrectly()
    {
        var (connection, dbPath) = CreateTestDb();
        try
        {
            // Common reserved words as column names
            VerifyReusableWorkflow12(
                input0: Fixture_ColumnName_IsReservedWord_HandledCorrectly_1,
                input1: ApplySchemaAndAssertSuccess,
                input2: connection
            );

            var inspected = SqliteTestDb.Inspect(connection);
            var table = inspected.Tables.Single();
            Assert.Equal(7, table.Columns.Count);
        }
        finally
        {
            CleanupTestDb(connection, dbPath);
        }
    }

    [Fact]
    public void TableName_CamelCase_PreservedCorrectly()
    {
        var (connection, dbPath) = CreateTestDb();
        try
        {
            var inspected = VerifyCrossClassChecks5(
                input0: Fixture_TableName_CamelCase_PreservedCorrectly_1,
                input1: connection,
                input2: _logger
            );
            Assert.Contains(inspected.Tables, t => t.Name == "UserAccountSettings");
        }
        finally
        {
            CleanupTestDb(connection, dbPath);
        }
    }

    // Implements [CI-TEST-SHARED-CASE-MATRIX].
    [Theory]
    [MemberData(nameof(ColumnName_WithNumbers_Success_CasesData))]
    public void ColumnName_WithNumbers_Success_Cases(string caseName, Func<SchemaDefinition> input0)
    {
        Assert.False(condition: string.IsNullOrEmpty(value: caseName));
        ExerciseCaseMatrix1(input0: input0);
    }

    #endregion

    #region Extreme Column Counts and Sizes

    [Fact]
    public void Table_ManyColumns_Success()
    {
        var (connection, dbPath) = CreateTestDb();
        try
        {
            // Build wide table with many columns
            VerifyReusableWorkflow12(
                input0: Fixture_Table_ManyColumns_Success_1,
                input1: ApplySchemaAndAssertSuccess,
                input2: connection
            );

            var inspected = SqliteTestDb.Inspect(connection);
            Assert.Equal(21, inspected.Tables.Single().Columns.Count);
        }
        finally
        {
            CleanupTestDb(connection, dbPath);
        }
    }

    #endregion

    #region Complex Constraints

    // Implements [CI-TEST-SHARED-CASE-MATRIX].
    [Theory]
    [MemberData(nameof(IndependentCommentScenarioData4))]
    public void Table_MultiColumnUniqueConstraint_Success_CommentScenarioCases(
        string scenarioName,
        global::System.Func<global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition> scenario0
    )
    {
        Assert.False(string.IsNullOrEmpty(scenarioName));
        VerifySharedPhase6(input0: scenario0);
    }

    [Fact]
    public void Table_MultiColumnIndex_Success()
    {
        var (connection, dbPath) = CreateTestDb();
        try
        {
            var inspected = VerifyCrossClassChecks5(
                input0: Fixture_Table_MultiColumnIndex_Success_1,
                input1: connection,
                input2: _logger
            );
            Assert.Single(inspected.Tables.Single().Indexes);
        }
        finally
        {
            CleanupTestDb(connection, dbPath);
        }
    }

    [Fact]
    public void Table_SelfReferencingForeignKey_Success()
    {
        var (connection, dbPath) = CreateTestDb();
        try
        {
            // Enable foreign keys
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "PRAGMA foreign_keys = ON";
                cmd.ExecuteNonQuery();
            }

            var inspected = VerifyCrossClassChecks5(
                input0: Fixture_Table_SelfReferencingForeignKey_Success_1,
                input1: connection,
                input2: _logger
            );
            var table = inspected.Tables.Single();
            Assert.Single(table.ForeignKeys);
            Assert.Equal("Categories", table.ForeignKeys[0].ReferencedTable);
        }
        finally
        {
            CleanupTestDb(connection, dbPath);
        }
    }

    [Fact]
    public void Table_MultipleIndexesOnSameColumn_Success()
    {
        var (connection, dbPath) = CreateTestDb();
        try
        {
            var inspected = VerifyCrossClassChecks5(
                input0: Fixture_Table_MultipleIndexesOnSameColumn_Success_1,
                input1: connection,
                input2: _logger
            );
            Assert.Equal(3, inspected.Tables.Single().Indexes.Count);
        }
        finally
        {
            CleanupTestDb(connection, dbPath);
        }
    }

    #endregion

    #region Nullable vs NotNull Edge Cases

    [Fact]
    public void AllColumnsNullable_ExceptPrimaryKey_Success()
    {
        var (connection, dbPath) = CreateTestDb();
        try
        {
            VerifySharedChecks4(
                input0: Fixture_AllColumnsNullable_ExceptPrimaryKey_Success_1,
                input1: connection
            );

            // Verify all columns except Id are nullable
            var inspected = SqliteTestDb.Inspect(connection);
            var table = inspected.Tables.Single();
            foreach (var col in table.Columns.Where(c => c.Name != "Id"))
            {
                Assert.True(col.IsNullable, $"Column {col.Name} should be nullable");
            }
        }
        finally
        {
            CleanupTestDb(connection, dbPath);
        }
    }

    #endregion

    #region Default Value Edge Cases


    #endregion

    #region Empty and Edge Schemas

    [Fact]
    public void EmptySchema_NoOperations()
    {
        var (connection, dbPath) = CreateTestDb();
        try
        {
            var schema = Schema.Define("Empty").Build();

            var operations = VerifyCrossClassChecks4(
                input0: connection,
                input1: _logger,
                input2: schema
            );

            Assert.Empty(operations);
        }
        finally
        {
            CleanupTestDb(connection, dbPath);
        }
    }

    [Fact]
    public void TableWithOnlyPrimaryKey_Success()
    {
        VerifySharedPhase6(input0: Data_TableWithOnlyPrimaryKey_Success_1);
    }

    [Fact]
    public void MultipleTables_CircularForeignKeys_DeferredConstraints()
    {
        // This tests a common real-world scenario where tables reference each other
        var (connection, dbPath) = CreateTestDb();
        try
        {
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "PRAGMA foreign_keys = ON";
                cmd.ExecuteNonQuery();
            }

            // Create tables without FK first, then add FKs
            VerifyReusableWorkflow12(
                input0: Fixture_MultipleTables_CircularForeignKeys_DeferredConstraints_1,
                input1: ApplySchemaAndAssertSuccess,
                input2: connection
            );
        }
        finally
        {
            CleanupTestDb(connection, dbPath);
        }
    }

    #endregion

    #region Upgrade Path Edge Cases

    [Fact]
    public void UpgradeFrom_EmptyTable_ToFullSchema_Success()
    {
        var (connection, dbPath) = CreateTestDb();
        try
        {
            // Start with minimal table
            var v1 = Data_UpgradeFrom_EmptyTable_ToFullSchema_Success_1();

            ApplySchema(connection, v1);

            // Upgrade to full table
            var v2 = Fixture_UpgradeFrom_EmptyTable_ToFullSchema_Success_1();

            var operations = VerifyCrossClassChecks4(
                input0: connection,
                input1: _logger,
                input2: v2
            );

            // Should have 4 AddColumn + 2 CreateIndex
            Assert.Equal(6, operations.Count);
            Assert.Equal(4, operations.Count(op => op is AddColumnOperation));
            Assert.Equal(2, operations.Count(op => op is CreateIndexOperation));

            global::Tests.Shared.ResultAssertions.Ok(
                actual: SqliteTestDb.TryApply(
                    connection: connection,
                    operations: operations,
                    logger: _logger
                )
            );
        }
        finally
        {
            CleanupTestDb(connection, dbPath);
        }
    }

    [Fact]
    public void AddIndex_ThenAddAnother_Success()
    {
        var (connection, dbPath) = CreateTestDb();
        try
        {
            // V1 with one index
            var v1 = Fixture_AddIndex_ThenAddAnother_Success_1();

            ApplySchema(connection, v1);

            // V2 - add another index (additive change)
            var operations = VerifySharedPhase2(
                input0: Fixture_AddIndex_ThenAddAnother_Success_2,
                input1: connection,
                input2: _logger,
                value0: false
            );

            // Should add the new index
            Assert.Single(operations);
            Assert.IsType<CreateIndexOperation>(operations[0]);

            var finalSchema = VerifyReusableWorkflow32(
                input0: connection,
                input1: operations,
                input2: _logger
            );
            Assert.Equal(2, finalSchema.Tables.Single().Indexes.Count);
        }
        finally
        {
            CleanupTestDb(connection, dbPath);
        }
    }

    #endregion

    #region Identity/AutoIncrement Edge Cases


    #endregion

    #region Helper Methods

    private Outcome.Result<bool, MigrationError> ApplySchema(
        SqliteConnection connection,
        SchemaDefinition schema
    )
    {
        var operations = SqliteTestDb.CalculateChanges(connection, schema, _logger);

        return SqliteTestDb.TryApply(
            connection: connection,
            operations: operations,
            logger: _logger
        );
    }

    private static void VerifyTableExists(SqliteConnection connection, string tableName)
    {
        var inspected = SqliteTestDb.Inspect(connection);
        Assert.Contains(inspected.Tables, t => t.Name == tableName);
    }

    #endregion

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private void ApplySchemaAndAssertSuccess(
        global::Microsoft.Data.Sqlite.SqliteConnection input0,
        global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition input1
    )
    {
        global::Tests.Shared.ResultAssertions.Ok(actual: ApplySchema(input0, input1));
    }

    // Implements [CI-TEST-SHARED-CASE-MATRIX].
    private void ExerciseCaseMatrix1(Func<SchemaDefinition> input0)
    {
        VerifySharedPhase6(input0: input0);
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private void VerifySharedChecks4(Func<SchemaDefinition> input0, SqliteConnection input1)
    {
        VerifyReusableWorkflow12(
            input0: input0,
            input1: ApplySchemaAndAssertSuccess,
            input2: input1
        );
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    internal static global::System.Collections.Generic.IReadOnlyList<global::Nimblesite.DataProvider.Migration.Core.SchemaOperation> VerifyCrossClassChecks4(
        SqliteConnection input0,
        ILogger input1,
        SchemaDefinition input2
    )
    {
        var operations = SqliteTestDb.CalculateChanges(input0, input2, input1);

        return operations;
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    internal global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition VerifyCrossClassChecks5(
        Func<SchemaDefinition> input0,
        SqliteConnection input1,
        ILogger input2
    )
    {
        VerifySharedChecks4(input0: input0, input1: input1);

        var inspected = (Ok(actual: SqliteSchemaInspector.Inspect(input1, input2))).Value;

        return inspected;
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    internal static IReadOnlyList<SchemaOperation> VerifySharedPhase2(
        Func<SchemaDefinition> input0,
        SqliteConnection input1,
        ILogger input2,
        bool value0
    )
    {
        // V2 - add another index (additive change)
        var v2 = input0();

        var operations = SqliteTestDb.CalculateChanges(
            inspect: () => (Ok(actual: SqliteSchemaInspector.Inspect(input1, input2))).Value,
            desired: v2,
            allowDestructive: value0,
            logger: input2
        );

        return operations;
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    internal void VerifySharedPhase6(Func<SchemaDefinition> input0)
    {
        var (connection, dbPath) = CreateTestDb();
        try
        {
            // Test multi-column unique constraint (composite PK requires different builder API)
            VerifyReusableWorkflow12(
                input0: input0,
                input1: ApplySchemaAndAssertSuccess,
                input2: connection
            );
        }
        finally
        {
            CleanupTestDb(connection, dbPath);
        }
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    // Implements [CI-TEST-SHARED-SCENARIOS].
    private static void VerifyReusableWorkflow12(
        Func<SchemaDefinition> input0,
        Action<SqliteConnection, SchemaDefinition> input1,
        SqliteConnection input2
    )
    { // Common reserved words as column names
        var schema = input0();
        input1(arg1: input2, arg2: schema);
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    internal static SchemaDefinition VerifyReusableWorkflow32(
        SqliteConnection input0,
        IReadOnlyList<SchemaOperation> input1,
        ILogger input2
    )
    {
        global::Tests.Shared.ResultAssertions.Ok(
            actual: SqliteTestDb.TryApply(connection: input0, operations: input1, logger: input2)
        );

        var finalSchema = (Ok(actual: SqliteSchemaInspector.Inspect(input0, input2))).Value;

        return finalSchema;
    }
}
