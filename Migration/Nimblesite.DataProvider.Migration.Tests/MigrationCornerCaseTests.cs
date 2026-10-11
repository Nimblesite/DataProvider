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
        if (File.Exists(dbPath))
        {
            try
            {
                File.Delete(dbPath);
            }
            catch
            {
                /* File may be locked */
            }
        }
    }

    #region Special Characters and Reserved Words

    [Fact]
    public void TableName_WithUnderscores_Success()
    {
        var (connection, dbPath) = CreateTestDb();
        try
        {
            var schema = Fixture_TableName_WithUnderscores_Success_1();

            var result = ApplySchema(connection, schema);

            Assert.True(result is MigrationApplyResultOk);
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
            var schema = Fixture_ColumnName_IsReservedWord_HandledCorrectly_1();

            var result = ApplySchema(connection, schema);

            Assert.True(result is MigrationApplyResultOk);

            var inspected = (
                (SchemaResultOk)SqliteSchemaInspector.Inspect(connection, _logger)
            ).Value;
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
            var schema = Fixture_TableName_CamelCase_PreservedCorrectly_1();

            var result = ApplySchema(connection, schema);

            Assert.True(result is MigrationApplyResultOk);

            var inspected = (
                (SchemaResultOk)SqliteSchemaInspector.Inspect(connection, _logger)
            ).Value;
            Assert.Contains(inspected.Tables, t => t.Name == "UserAccountSettings");
        }
        finally
        {
            CleanupTestDb(connection, dbPath);
        }
    }

    [Fact]
    public void ColumnName_WithNumbers_Success()
    {
        var (connection, dbPath) = CreateTestDb();
        try
        {
            var schema = Fixture_ColumnName_WithNumbers_Success_1();

            var result = ApplySchema(connection, schema);

            Assert.True(result is MigrationApplyResultOk);
        }
        finally
        {
            CleanupTestDb(connection, dbPath);
        }
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
            var schema = Fixture_Table_ManyColumns_Success_1();

            var result = ApplySchema(connection, schema);

            Assert.True(result is MigrationApplyResultOk);

            var inspected = (
                (SchemaResultOk)SqliteSchemaInspector.Inspect(connection, _logger)
            ).Value;
            Assert.Equal(21, inspected.Tables.Single().Columns.Count);
        }
        finally
        {
            CleanupTestDb(connection, dbPath);
        }
    }

    [Fact]
    public void Column_MaximumVarCharLength_Success()
    {
        var (connection, dbPath) = CreateTestDb();
        try
        {
            var schema = Fixture_Column_MaximumVarCharLength_Success_1();

            var result = ApplySchema(connection, schema);

            Assert.True(result is MigrationApplyResultOk);
        }
        finally
        {
            CleanupTestDb(connection, dbPath);
        }
    }

    [Fact]
    public void Decimal_ExtremeScaleAndPrecision_Success()
    {
        var (connection, dbPath) = CreateTestDb();
        try
        {
            var schema = Fixture_Decimal_ExtremeScaleAndPrecision_Success_1();

            var result = ApplySchema(connection, schema);

            Assert.True(result is MigrationApplyResultOk);
        }
        finally
        {
            CleanupTestDb(connection, dbPath);
        }
    }

    #endregion

    #region Complex Constraints

    [Fact]
    public void Table_MultiColumnUniqueConstraint_Success()
    {
        var (connection, dbPath) = CreateTestDb();
        try
        {
            // Test multi-column unique constraint (composite PK requires different builder API)
            var schema = Fixture_Table_MultiColumnUniqueConstraint_Success_1();

            var result = ApplySchema(connection, schema);

            Assert.True(result is MigrationApplyResultOk);
        }
        finally
        {
            CleanupTestDb(connection, dbPath);
        }
    }

    [Fact]
    public void Table_MultiColumnIndex_Success()
    {
        var (connection, dbPath) = CreateTestDb();
        try
        {
            var schema = Fixture_Table_MultiColumnIndex_Success_1();

            var result = ApplySchema(connection, schema);

            Assert.True(result is MigrationApplyResultOk);

            var inspected = (
                (SchemaResultOk)SqliteSchemaInspector.Inspect(connection, _logger)
            ).Value;
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

            var schema = Fixture_Table_SelfReferencingForeignKey_Success_1(
                table: table,
                idType: idType,
                nameType: nameType
            );

            var result = ApplySchema(connection, schema);

            Assert.True(result is MigrationApplyResultOk);

            var inspected = (
                (SchemaResultOk)SqliteSchemaInspector.Inspect(connection, _logger)
            ).Value;
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
            var schema = Fixture_Table_MultipleIndexesOnSameColumn_Success_1();

            var result = ApplySchema(connection, schema);

            Assert.True(result is MigrationApplyResultOk);

            var inspected = (
                (SchemaResultOk)SqliteSchemaInspector.Inspect(connection, _logger)
            ).Value;
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
            var schema = Fixture_AllColumnsNullable_ExceptPrimaryKey_Success_1();

            var result = ApplySchema(connection, schema);

            Assert.True(result is MigrationApplyResultOk);

            // Verify all columns except Id are nullable
            var inspected = (
                (SchemaResultOk)SqliteSchemaInspector.Inspect(connection, _logger)
            ).Value;
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

    [Fact]
    public void AllColumnsNotNull_Success()
    {
        var (connection, dbPath) = CreateTestDb();
        try
        {
            var schema = Fixture_AllColumnsNotNull_Success_1(
                table: table,
                idType: idType,
                nameType: nameType
            );

            var result = ApplySchema(connection, schema);

            Assert.True(result is MigrationApplyResultOk);
        }
        finally
        {
            CleanupTestDb(connection, dbPath);
        }
    }

    #endregion

    #region Default Value Edge Cases

    [Fact]
    public void DefaultValue_StringWithQuotes_Success()
    {
        var (connection, dbPath) = CreateTestDb();
        try
        {
            var schema = Fixture_DefaultValue_StringWithQuotes_Success_1();

            var result = ApplySchema(connection, schema);

            Assert.True(result is MigrationApplyResultOk);
        }
        finally
        {
            CleanupTestDb(connection, dbPath);
        }
    }

    [Fact]
    public void DefaultValue_NumericZero_Success()
    {
        var (connection, dbPath) = CreateTestDb();
        try
        {
            var schema = Fixture_DefaultValue_NumericZero_Success_1();

            var result = ApplySchema(connection, schema);

            Assert.True(result is MigrationApplyResultOk);
        }
        finally
        {
            CleanupTestDb(connection, dbPath);
        }
    }

    [Fact]
    public void DefaultValue_BooleanFalse_Success()
    {
        var (connection, dbPath) = CreateTestDb();
        try
        {
            var schema = Fixture_DefaultValue_BooleanFalse_Success_1();

            var result = ApplySchema(connection, schema);

            Assert.True(result is MigrationApplyResultOk);
        }
        finally
        {
            CleanupTestDb(connection, dbPath);
        }
    }

    [Fact]
    public void DefaultValue_CurrentTimestamp_Success()
    {
        var (connection, dbPath) = CreateTestDb();
        try
        {
            var schema = Fixture_DefaultValue_CurrentTimestamp_Success_1();

            var result = ApplySchema(connection, schema);

            Assert.True(result is MigrationApplyResultOk);
        }
        finally
        {
            CleanupTestDb(connection, dbPath);
        }
    }

    #endregion

    #region Empty and Edge Schemas

    [Fact]
    public void EmptySchema_NoOperations()
    {
        var (connection, dbPath) = CreateTestDb();
        try
        {
            var schema = Schema.Define("Empty").Build();

            var emptyDbSchema = (
                (SchemaResultOk)SqliteSchemaInspector.Inspect(connection, _logger)
            ).Value;

            var operations = (
                (OperationsResultOk)SchemaDiff.Calculate(emptyDbSchema, schema, logger: _logger)
            ).Value;

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
        var (connection, dbPath) = CreateTestDb();
        try
        {
            var schema = Schema
                .Define("Test")
                .Table("Simple", t => t.Column("Id", PortableTypes.Uuid, c => c.PrimaryKey()))
                .Build();

            var result = ApplySchema(connection, schema);

            Assert.True(result is MigrationApplyResultOk);
        }
        finally
        {
            CleanupTestDb(connection, dbPath);
        }
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
            var schema = Fixture_MultipleTables_CircularForeignKeys_DeferredConstraints_1();

            var result = ApplySchema(connection, schema);

            Assert.True(result is MigrationApplyResultOk);
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
            var v1 = Schema
                .Define("Test")
                .Table("Products", t => t.Column("Id", PortableTypes.Int, c => c.PrimaryKey()))
                .Build();

            ApplySchema(connection, v1);

            // Upgrade to full table
            var v2 = Fixture_UpgradeFrom_EmptyTable_ToFullSchema_Success_1(
                table: table,
                idType: idType,
                nameType: nameType
            );

            var currentSchema = (
                (SchemaResultOk)SqliteSchemaInspector.Inspect(connection, _logger)
            ).Value;

            var operations = (
                (OperationsResultOk)SchemaDiff.Calculate(currentSchema, v2, logger: _logger)
            ).Value;

            // Should have 4 AddColumn + 2 CreateIndex
            Assert.Equal(6, operations.Count);
            Assert.Equal(4, operations.Count(op => op is AddColumnOperation));
            Assert.Equal(2, operations.Count(op => op is CreateIndexOperation));

            var result = SqliteTestDb.TryApply(
                connection: connection,
                operations: operations,
                logger: _logger
            );

            Assert.True(result is MigrationApplyResultOk);
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
            var v2 = Fixture_AddIndex_ThenAddAnother_Success_2();

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

            // Should add the new index
            Assert.Single(operations);
            Assert.IsType<CreateIndexOperation>(operations[0]);

            var result = SqliteTestDb.TryApply(
                connection: connection,
                operations: operations,
                logger: _logger
            );

            Assert.True(result is MigrationApplyResultOk);

            var finalSchema = (
                (SchemaResultOk)SqliteSchemaInspector.Inspect(connection, _logger)
            ).Value;
            Assert.Equal(2, finalSchema.Tables.Single().Indexes.Count);
        }
        finally
        {
            CleanupTestDb(connection, dbPath);
        }
    }

    #endregion

    #region Identity/AutoIncrement Edge Cases

    [Fact]
    public void Table_MultipleIdentityColumns_OnePerTable()
    {
        var (connection, dbPath) = CreateTestDb();
        try
        {
            // SQLite only allows one ROWID alias (INTEGER PRIMARY KEY)
            var schema = Fixture_Table_MultipleIdentityColumns_OnePerTable_1();

            var result = ApplySchema(connection, schema);

            Assert.True(result is MigrationApplyResultOk);
        }
        finally
        {
            CleanupTestDb(connection, dbPath);
        }
    }

    #endregion

    #region Helper Methods

    private Outcome.Result<bool, MigrationError> ApplySchema(
        SqliteConnection connection,
        SchemaDefinition schema
    )
    {
        var currentSchema = (
            (SchemaResultOk)SqliteSchemaInspector.Inspect(connection, _logger)
        ).Value;

        var operations = (
            (OperationsResultOk)SchemaDiff.Calculate(currentSchema, schema, logger: _logger)
        ).Value;

        return SqliteTestDb.TryApply(
            connection: connection,
            operations: operations,
            logger: _logger
        );
    }

    private void VerifyTableExists(SqliteConnection connection, string tableName)
    {
        var inspected = ((SchemaResultOk)SqliteSchemaInspector.Inspect(connection, _logger)).Value;
        Assert.Contains(inspected.Tables, t => t.Name == tableName);
    }

    #endregion
}
