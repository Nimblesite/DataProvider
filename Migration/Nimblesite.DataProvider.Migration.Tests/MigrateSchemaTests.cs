// Implements [CI-TEST-SHARED-COMMAND].

namespace Nimblesite.DataProvider.Migration.Tests;

/// <summary>
/// Tests for PostgresDdlGenerator.MigrateSchema() method.
/// Covers: drop schema, fresh migration, partial upgrade scenarios.
/// Uses the shared postgres container; each test gets its own database.
/// </summary>
[Collection(PostgresTestSuite.Name)]
public sealed partial class MigrateSchemaTests(PostgresContainerFixture fixture)
    : PostgresDatabaseTest(fixture)
{
    protected override string DatabaseName => "migrate_schema_test";

    /// <summary>
    /// Test schema definition for migration tests.
    /// </summary>
    [Fact]
    public void MigrateSchema_FreshDatabase_CreatesAllTables()
    {
        // Arrange
        var tablesCreated = new List<string>();

        // Act
        MigrateAndAssertCreated(tablesCreated: tablesCreated);

        // Assert
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
        MigrateAndAssertSuccess(schema: schema, expectedTablesCreated: 4);

        // Act - Run migration again
        MigrateAndAssertSuccess(schema: schema, expectedTablesCreated: 4);
    }

    [Fact]
    public void MigrateSchema_PartiallyMigrated_CreatesRemainingTables()
    {
        // Arrange - Create only the first two tables manually
        ExecuteSql(Data_MigrateSchema_PartiallyMigrated_CreatesRemainingTables_1);

        ExecuteSql(Data_MigrateSchema_PartiallyMigrated_CreatesRemainingTables_2);

        Assert.True(TableExists("countries"));
        Assert.True(TableExists("regions"));
        Assert.False(TableExists("suburbs"));
        Assert.False(TableExists("venues"));

        var tablesCreated = new List<string>();

        // Act - Run migration on partially migrated database
        MigrateAndAssertCreated(tablesCreated: tablesCreated);

        // Assert
        // All tables should now exist
        AssertTestTablesExist();
    }

    [Fact]
    public void MigrateSchema_TableCreationFails_ContinuesWithOtherTables()
    {
        // Arrange - Create a conflicting table that will cause FK failure
        // Create suburbs without its parent (regions) - this will cause venues to fail FK
        ExecuteSql(Data_MigrateSchema_TableCreationFails_ContinuesWithOtherTables_1);

        // Create a schema where one table references a non-existent table
        var schemaWithBadFk = Fixture_MigrateSchema_TableCreationFails_ContinuesWithOtherTables_1();

        var failedTables = new List<string>();

        // Act
        var result = PostgresDdlGenerator.MigrateSchema(
            Connection,
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
        var schemaWithBadTable = Fixture_MigrateSchema_CallbacksInvoked_ForSuccessAndFailure_1();

        var createdTables = new List<string>();
        var failedTables = new List<(string Name, Exception Ex)>();

        // Act
        var result = PostgresDdlGenerator.MigrateSchema(
            Connection,
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
        MigrateAndAssertSuccess(schema: schema, expectedTablesCreated: 4);
        Assert.True(TableExists("countries"));
        Assert.True(TableExists("venues"));

        // Act - Drop all tables in reverse order (respecting FK constraints)
        VerifyReusableWorkflow29(input0: schema, input1: ExecuteSql, input2: TableExists);
        Assert.False(TableExists("regions"));
        Assert.False(TableExists("suburbs"));
        Assert.False(TableExists("venues"));
    }

    [Fact]
    public void MigrateSchema_AfterDrop_RecreatesAllTables()
    {
        // Arrange - Create and drop schema
        var schema = CreateTestSchema();
        _ = PostgresDdlGenerator.MigrateSchema(Connection, schema);

        // Drop all tables
        VerifyReusableWorkflow29(input0: schema, input1: ExecuteSql, input2: TableExists);

        // Act - Migrate again
        MigrateAndAssertSuccess(schema: schema, expectedTablesCreated: 4);
        AssertTestTablesExist();
    }

    [Fact]
    public void MigrateSchema_WithIndexes_CreatesIndexes()
    {
        // Arrange
        var schema = CreateTestSchema();

        // Act
        MigrateAndAssertSuccess(schema: schema, expectedTablesCreated: 4);

        // Assert
        Assert.True(IndexExists("idx_regions_country"));
    }

    [Fact]
    public void MigrateSchema_EmptySchema_ReturnsSuccess()
    {
        // Arrange
        var emptySchema = Schema.Define("Empty").Build();

        // Act
        MigrateAndAssertSuccess(schema: emptySchema, expectedTablesCreated: 0);
    }

    // Implements [MIG-TEST-TABLE-ASSERTIONS].
    private void MigrateAndAssertSuccess(SchemaDefinition schema, int expectedTablesCreated)
    {
        var result = PostgresDdlGenerator.MigrateSchema(Connection, schema);
        Assert.True(result.Success);
        Assert.Equal(expectedTablesCreated, result.TablesCreated);
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

    private void MigrateAndAssertCreated(List<string> tablesCreated)
    {
        var result = PostgresDdlGenerator.MigrateSchema(
            Connection,
            CreateTestSchema(),
            onTableCreated: tablesCreated.Add
        );

        Assert.True(
            result.Success,
            $"Migration failed with errors: {string.Join(", ", result.Errors)}"
        );
        Assert.Equal(4, result.TablesCreated);
        Assert.Empty(result.Errors);
    }

    private bool TableExists(string tableName) =>
        ScalarExists(sql: Data_TableExists_1, parameterName: "@tableName", value: tableName);

    private bool IndexExists(string indexName) =>
        ScalarExists(sql: Data_IndexExists_1, parameterName: "@indexName", value: indexName);

    private bool ScalarExists(string sql, string parameterName, string value)
    {
        using var cmd = Connection.CreateCommand();
        cmd.CommandText = sql;
        global::Tests.Shared.DatabaseCommands.Bind(command: cmd, name: parameterName, value: value);
        return (bool)cmd.ExecuteScalar()!;
    }

    private void ExecuteSql(string sql)
    {
        DatabaseCommands.Execute(
            connection: Connection,
            configure: command => command.CommandText = sql
        );
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    // Implements [CI-TEST-SHARED-SCENARIOS].
    private static void VerifyReusableWorkflow29(
        SchemaDefinition input0,
        Action<string> input1,
        Func<string, bool> input2
    )
    {
        // Act - Drop all tables in reverse order (respecting FK constraints)
        var tables = input0.Tables.Reverse().ToList();
        foreach (var table in tables)
        {
            input1(obj: $"DROP TABLE IF EXISTS \"{table.Schema}\".\"{table.Name}\" CASCADE");
        }

        // Assert
        Assert.False(input2(arg: "countries"));
    }
}
