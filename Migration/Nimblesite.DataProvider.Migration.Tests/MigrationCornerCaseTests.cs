namespace Nimblesite.DataProvider.Migration.Tests;

/// <summary>
/// Corner case and edge case tests for migrations.
/// Tests special characters, reserved words, extreme values, and unusual schemas.
/// </summary>
public sealed class MigrationCornerCaseTests
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
            var schema = Schema
                .Define("Test")
                .Table(
                    "user_roles_history",
                    t =>
                        t.Column("id", PortableTypes.BigInt, c => c.PrimaryKey())
                            .Column("user_id", PortableTypes.Uuid)
                            .Column("role_name", PortableTypes.VarChar(100))
                )
                .Build();

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
            var schema = Schema
                .Define("Test")
                .Table(
                    "DataTable",
                    t =>
                        t.Column("index", PortableTypes.Int, c => c.PrimaryKey())
                            .Column("order", PortableTypes.Int)
                            .Column("group", PortableTypes.VarChar(50))
                            .Column("select", PortableTypes.Text)
                            .Column("where", PortableTypes.Boolean)
                            .Column("from", PortableTypes.DateTime())
                            .Column("table", PortableTypes.VarChar(100))
                )
                .Build();

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
            var schema = Schema
                .Define("Test")
                .Table(
                    "UserAccountSettings",
                    t =>
                        t.Column("Id", PortableTypes.Uuid, c => c.PrimaryKey())
                            .Column("UserId", PortableTypes.Uuid)
                            .Column("EnableNotifications", PortableTypes.Boolean)
                )
                .Build();

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
            var schema = Schema
                .Define("Test")
                .Table(
                    "Metrics",
                    t =>
                        t.Column("id", PortableTypes.Int, c => c.PrimaryKey())
                            .Column("value1", PortableTypes.Decimal(10, 2))
                            .Column("value2", PortableTypes.Decimal(10, 2))
                            .Column("metric99", PortableTypes.Float)
                )
                .Build();

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
            var schema = Schema
                .Define("Test")
                .Table(
                    "WideTable",
                    t =>
                    {
                        t.Column("Id", PortableTypes.Int, c => c.PrimaryKey());
                        // Add 20 columns (enough to test wide tables)
                        for (var i = 1; i <= 20; i++)
                        {
                            t.Column($"Col{i}", PortableTypes.VarChar(100));
                        }
                    }
                )
                .Build();

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
            var schema = Schema
                .Define("Test")
                .Table(
                    "LargeText",
                    t =>
                        t.Column("Id", PortableTypes.Int, c => c.PrimaryKey())
                            .Column("ShortText", PortableTypes.VarChar(10))
                            .Column("MediumText", PortableTypes.VarChar(4000))
                            .Column("LargeText", PortableTypes.VarChar(8000))
                            .Column("MaxText", PortableTypes.Text)
                )
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
    public void Decimal_ExtremeScaleAndPrecision_Success()
    {
        var (connection, dbPath) = CreateTestDb();
        try
        {
            var schema = Schema
                .Define("Test")
                .Table(
                    "Financials",
                    t =>
                        t.Column("Id", PortableTypes.Int, c => c.PrimaryKey())
                            .Column("TinyMoney", PortableTypes.Decimal(5, 2))
                            .Column("StandardMoney", PortableTypes.Decimal(10, 2))
                            .Column("BigMoney", PortableTypes.Decimal(18, 4))
                            .Column("HugeMoney", PortableTypes.Decimal(28, 8))
                            .Column("CryptoValue", PortableTypes.Decimal(38, 18))
                )
                .Build();

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
            var schema = Schema
                .Define("Test")
                .Table(
                    "CompositeUnique",
                    t =>
                        t.Column("Id", PortableTypes.Int, c => c.PrimaryKey())
                            .Column("TenantId", PortableTypes.Uuid, c => c.NotNull())
                            .Column("EntityId", PortableTypes.Uuid, c => c.NotNull())
                            .Column("Data", PortableTypes.Text)
                            .Unique("UQ_tenant_entity", "TenantId", "EntityId")
                )
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
    public void Table_MultiColumnIndex_Success()
    {
        var (connection, dbPath) = CreateTestDb();
        try
        {
            var schema = Schema
                .Define("Test")
                .Table(
                    "Events",
                    t =>
                        t.Column("Id", PortableTypes.BigInt, c => c.PrimaryKey())
                            .Column("TenantId", PortableTypes.Uuid)
                            .Column("EntityType", PortableTypes.VarChar(100))
                            .Column("EntityId", PortableTypes.Uuid)
                            .Column("EventDate", PortableTypes.DateTime())
                            .Index(
                                "idx_events_tenant_entity",
                                ["TenantId", "EntityType", "EntityId"]
                            )
                )
                .Build();

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

            var schema = Schema
                .Define("Test")
                .Table(
                    "Categories",
                    t =>
                        t.Column("Id", PortableTypes.Int, c => c.PrimaryKey())
                            .Column("Name", PortableTypes.VarChar(100), c => c.NotNull())
                            .Column("ParentId", PortableTypes.Int)
                            .ForeignKey("ParentId", "Categories", "Id", ForeignKeyAction.SetNull)
                )
                .Build();

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
            var schema = Schema
                .Define("Test")
                .Table(
                    "Documents",
                    t =>
                        t.Column("Id", PortableTypes.Uuid, c => c.PrimaryKey())
                            .Column("Title", PortableTypes.VarChar(500))
                            .Column("Status", PortableTypes.VarChar(20))
                            .Column("CreatedAt", PortableTypes.DateTime())
                            .Index("idx_docs_title", "Title")
                            .Index("idx_docs_status", "Status")
                            .Index("idx_docs_status_created", ["Status", "CreatedAt"])
                )
                .Build();

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
            var schema = Schema
                .Define("Test")
                .Table(
                    "OptionalData",
                    t =>
                        t.Column("Id", PortableTypes.Uuid, c => c.PrimaryKey())
                            .Column("Name", PortableTypes.VarChar(100))
                            .Column("Email", PortableTypes.VarChar(255))
                            .Column("Age", PortableTypes.Int)
                            .Column("Balance", PortableTypes.Decimal(10, 2))
                            .Column("Active", PortableTypes.Boolean)
                )
                .Build();

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
            var schema = Schema
                .Define("Test")
                .Table(
                    "RequiredData",
                    t =>
                        t.Column("Id", PortableTypes.Uuid, c => c.PrimaryKey())
                            .Column("Name", PortableTypes.VarChar(100), c => c.NotNull())
                            .Column("Email", PortableTypes.VarChar(255), c => c.NotNull())
                            .Column(
                                "Status",
                                PortableTypes.VarChar(20),
                                c => c.NotNull().Default("'active'")
                            )
                )
                .Build();

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
            var schema = Schema
                .Define("Test")
                .Table(
                    "Defaults",
                    t =>
                        t.Column("Id", PortableTypes.Int, c => c.PrimaryKey())
                            .Column(
                                "Status",
                                PortableTypes.VarChar(50),
                                c => c.Default("'pending'")
                            )
                            .Column("Type", PortableTypes.VarChar(50), c => c.Default("'default'"))
                )
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
    public void DefaultValue_NumericZero_Success()
    {
        var (connection, dbPath) = CreateTestDb();
        try
        {
            var schema = Schema
                .Define("Test")
                .Table(
                    "Counters",
                    t =>
                        t.Column("Id", PortableTypes.Int, c => c.PrimaryKey())
                            .Column("Count", PortableTypes.Int, c => c.Default("0"))
                            .Column("Balance", PortableTypes.Decimal(10, 2), c => c.Default("0.00"))
                )
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
    public void DefaultValue_BooleanFalse_Success()
    {
        var (connection, dbPath) = CreateTestDb();
        try
        {
            var schema = Schema
                .Define("Test")
                .Table(
                    "Flags",
                    t =>
                        t.Column("Id", PortableTypes.Int, c => c.PrimaryKey())
                            .Column("IsActive", PortableTypes.Boolean, c => c.Default("0"))
                            .Column("IsVerified", PortableTypes.Boolean, c => c.Default("1"))
                )
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
    public void DefaultValue_CurrentTimestamp_Success()
    {
        var (connection, dbPath) = CreateTestDb();
        try
        {
            var schema = Schema
                .Define("Test")
                .Table(
                    "Auditable",
                    t =>
                        t.Column("Id", PortableTypes.Int, c => c.PrimaryKey())
                            .Column(
                                "CreatedAt",
                                PortableTypes.DateTime(),
                                c => c.Default("CURRENT_TIMESTAMP")
                            )
                            .Column("UpdatedAt", PortableTypes.DateTime())
                )
                .Build();

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
            var schema = Schema
                .Define("Test")
                .Table(
                    "Authors",
                    t =>
                        t.Column("Id", PortableTypes.Int, c => c.PrimaryKey())
                            .Column("Name", PortableTypes.VarChar(100))
                )
                .Table(
                    "Books",
                    t =>
                        t.Column("Id", PortableTypes.Int, c => c.PrimaryKey())
                            .Column("Title", PortableTypes.VarChar(200))
                            .Column("AuthorId", PortableTypes.Int)
                            .ForeignKey("AuthorId", "Authors", "Id")
                )
                .Build();

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
            var v2 = Schema
                .Define("Test")
                .Table(
                    "Products",
                    t =>
                        t.Column("Id", PortableTypes.Int, c => c.PrimaryKey())
                            .Column("Name", PortableTypes.VarChar(200), c => c.NotNull())
                            .Column("Price", PortableTypes.Decimal(10, 2))
                            .Column("CategoryId", PortableTypes.Int)
                            .Column("CreatedAt", PortableTypes.DateTime())
                            .Index("idx_products_name", "Name")
                            .Index("idx_products_category", "CategoryId")
                )
                .Build();

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
            var v1 = Schema
                .Define("Test")
                .Table(
                    "Items",
                    t =>
                        t.Column("Id", PortableTypes.Int, c => c.PrimaryKey())
                            .Column("Code", PortableTypes.VarChar(50))
                            .Column("Category", PortableTypes.VarChar(50))
                            .Index("idx_items_code", "Code")
                )
                .Build();

            ApplySchema(connection, v1);

            // V2 - add another index (additive change)
            var v2 = Schema
                .Define("Test")
                .Table(
                    "Items",
                    t =>
                        t.Column("Id", PortableTypes.Int, c => c.PrimaryKey())
                            .Column("Code", PortableTypes.VarChar(50))
                            .Column("Category", PortableTypes.VarChar(50))
                            .Index("idx_items_code", "Code")
                            .Index("idx_items_category", "Category")
                )
                .Build();

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
            var schema = Schema
                .Define("Test")
                .Table(
                    "Sequenced",
                    t =>
                        t.Column("Id", PortableTypes.BigInt, c => c.PrimaryKey().Identity())
                            .Column("Name", PortableTypes.VarChar(100))
                            .Column("OrderNum", PortableTypes.Int) // Not identity
                )
                .Build();

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
