namespace Nimblesite.DataProvider.Migration.Tests;

/// <summary>
/// Tests for SchemaDiff.Calculate() method.
/// Covers: create tables, add columns, create indexes, add foreign keys, destructive operations.
/// </summary>
public sealed class SchemaDiffTests
{
    [Fact]
    public void Calculate_EmptyCurrentToNewDesired_CreatesTable()
    {
        // Arrange
        var current = Schema.Define("Current").Build();

        var desired = Schema
            .Define("Desired")
            .Table(
                "public",
                "users",
                t =>
                    t.Column("id", PortableTypes.Uuid, c => c.PrimaryKey())
                        .Column("name", PortableTypes.VarChar(100), c => c.NotNull())
            )
            .Build();

        // Act
        var result = SchemaDiff.Calculate(current, desired);

        // Assert
        Assert.True(result is OperationsResultOk);
        var ops = ((OperationsResultOk)result).Value;

        Assert.Single(ops);
        Assert.IsType<CreateTableOperation>(ops[0]);
        var createOp = (CreateTableOperation)ops[0];
        Assert.Equal("users", createOp.Table.Name);
    }

    [Fact]
    public void Calculate_SameSchema_NoOperations()
    {
        // Arrange
        var schema = Schema
            .Define("Test")
            .Table(
                "public",
                "users",
                t =>
                    t.Column("id", PortableTypes.Uuid, c => c.PrimaryKey())
                        .Column("name", PortableTypes.VarChar(100))
            )
            .Build();

        // Act
        var result = SchemaDiff.Calculate(schema, schema);

        // Assert
        Assert.True(result is OperationsResultOk);
        var ops = ((OperationsResultOk)result).Value;

        Assert.Empty(ops);
    }

    [Fact]
    public void Calculate_NewColumn_AddsColumn()
    {
        // Arrange
        var current = Schema
            .Define("Current")
            .Table("public", "users", t => t.Column("id", PortableTypes.Uuid, c => c.PrimaryKey()))
            .Build();

        var desired = Schema
            .Define("Desired")
            .Table(
                "public",
                "users",
                t =>
                    t.Column("id", PortableTypes.Uuid, c => c.PrimaryKey())
                        .Column("email", PortableTypes.VarChar(255), c => c.NotNull())
            )
            .Build();

        // Act
        var result = SchemaDiff.Calculate(current, desired);

        // Assert
        Assert.True(result is OperationsResultOk);
        var ops = ((OperationsResultOk)result).Value;

        Assert.Single(ops);
        Assert.IsType<AddColumnOperation>(ops[0]);
        var addColOp = (AddColumnOperation)ops[0];
        Assert.Equal("email", addColOp.Column.Name);
        Assert.Equal("users", addColOp.TableName);
    }

    [Fact]
    public void Calculate_NewIndex_CreatesIndex()
    {
        // Arrange
        var current = Schema
            .Define("Current")
            .Table(
                "public",
                "users",
                t =>
                    t.Column("id", PortableTypes.Uuid, c => c.PrimaryKey())
                        .Column("email", PortableTypes.VarChar(255))
            )
            .Build();

        var desired = Schema
            .Define("Desired")
            .Table(
                "public",
                "users",
                t =>
                    t.Column("id", PortableTypes.Uuid, c => c.PrimaryKey())
                        .Column("email", PortableTypes.VarChar(255))
                        .Index("idx_users_email", "email")
            )
            .Build();

        // Act
        var result = SchemaDiff.Calculate(current, desired);

        // Assert
        Assert.True(result is OperationsResultOk);
        var ops = ((OperationsResultOk)result).Value;

        Assert.Single(ops);
        Assert.IsType<CreateIndexOperation>(ops[0]);
        var createIdxOp = (CreateIndexOperation)ops[0];
        Assert.Equal("idx_users_email", createIdxOp.Index.Name);
    }

    [Fact]
    public void Calculate_NewForeignKey_AddsForeignKey()
    {
        // Arrange
        var current = Schema
            .Define("Current")
            .Table(
                "public",
                "departments",
                t => t.Column("id", PortableTypes.Uuid, c => c.PrimaryKey())
            )
            .Table(
                "public",
                "employees",
                t =>
                    t.Column("id", PortableTypes.Uuid, c => c.PrimaryKey())
                        .Column("dept_id", PortableTypes.Uuid)
            )
            .Build();

        var desired = Schema
            .Define("Desired")
            .Table(
                "public",
                "departments",
                t => t.Column("id", PortableTypes.Uuid, c => c.PrimaryKey())
            )
            .Table(
                "public",
                "employees",
                t =>
                    t.Column("id", PortableTypes.Uuid, c => c.PrimaryKey())
                        .Column("dept_id", PortableTypes.Uuid)
                        .ForeignKey("dept_id", "departments", "id", ForeignKeyAction.Cascade)
            )
            .Build();

        // Act
        var result = SchemaDiff.Calculate(current, desired);

        // Assert
        Assert.True(result is OperationsResultOk);
        var ops = ((OperationsResultOk)result).Value;

        Assert.Single(ops);
        Assert.IsType<AddForeignKeyOperation>(ops[0]);
        var addFkOp = (AddForeignKeyOperation)ops[0];
        Assert.Equal("employees", addFkOp.TableName);
    }

    [Fact]
    public void Calculate_RemovedTable_NotDroppedByDefault()
    {
        // Arrange
        var current = Schema
            .Define("Current")
            .Table("public", "users", t => t.Column("id", PortableTypes.Uuid, c => c.PrimaryKey()))
            .Table(
                "public",
                "obsolete",
                t => t.Column("id", PortableTypes.Uuid, c => c.PrimaryKey())
            )
            .Build();

        var desired = Schema
            .Define("Desired")
            .Table("public", "users", t => t.Column("id", PortableTypes.Uuid, c => c.PrimaryKey()))
            .Build();

        // Act
        var result = SchemaDiff.Calculate(current, desired, allowDestructive: false);

        // Assert
        Assert.True(result is OperationsResultOk);
        var ops = ((OperationsResultOk)result).Value;

        Assert.Empty(ops);
    }

    [Fact]
    public void Calculate_RemovedTable_DroppedWhenDestructiveAllowed()
    {
        // Arrange
        var current = Schema
            .Define("Current")
            .Table("public", "users", t => t.Column("id", PortableTypes.Uuid, c => c.PrimaryKey()))
            .Table(
                "public",
                "obsolete",
                t => t.Column("id", PortableTypes.Uuid, c => c.PrimaryKey())
            )
            .Build();

        var desired = Schema
            .Define("Desired")
            .Table("public", "users", t => t.Column("id", PortableTypes.Uuid, c => c.PrimaryKey()))
            .Build();

        // Act
        var result = SchemaDiff.Calculate(current, desired, allowDestructive: true);

        // Assert
        Assert.True(result is OperationsResultOk);
        var ops = ((OperationsResultOk)result).Value;

        Assert.Single(ops);
        Assert.IsType<DropTableOperation>(ops[0]);
        var dropOp = (DropTableOperation)ops[0];
        Assert.Equal("obsolete", dropOp.TableName);
    }

    [Fact]
    public void Calculate_RemovedColumn_NotDroppedByDefault()
    {
        // Arrange
        var current = Schema
            .Define("Current")
            .Table(
                "public",
                "users",
                t =>
                    t.Column("id", PortableTypes.Uuid, c => c.PrimaryKey())
                        .Column("old_field", PortableTypes.VarChar(100))
            )
            .Build();

        var desired = Schema
            .Define("Desired")
            .Table("public", "users", t => t.Column("id", PortableTypes.Uuid, c => c.PrimaryKey()))
            .Build();

        // Act
        var result = SchemaDiff.Calculate(current, desired, allowDestructive: false);

        // Assert
        Assert.True(result is OperationsResultOk);
        var ops = ((OperationsResultOk)result).Value;

        Assert.Empty(ops);
    }

    [Fact]
    public void Calculate_RemovedColumn_DroppedWhenDestructiveAllowed()
    {
        // Arrange
        var current = Schema
            .Define("Current")
            .Table(
                "public",
                "users",
                t =>
                    t.Column("id", PortableTypes.Uuid, c => c.PrimaryKey())
                        .Column("old_field", PortableTypes.VarChar(100))
            )
            .Build();

        var desired = Schema
            .Define("Desired")
            .Table("public", "users", t => t.Column("id", PortableTypes.Uuid, c => c.PrimaryKey()))
            .Build();

        // Act
        var result = SchemaDiff.Calculate(current, desired, allowDestructive: true);

        // Assert
        Assert.True(result is OperationsResultOk);
        var ops = ((OperationsResultOk)result).Value;

        Assert.Single(ops);
        Assert.IsType<DropColumnOperation>(ops[0]);
        var dropColOp = (DropColumnOperation)ops[0];
        Assert.Equal("old_field", dropColOp.ColumnName);
    }

    [Fact]
    public void Calculate_RemovedIndex_NotDroppedByDefault()
    {
        // Arrange
        var current = Schema
            .Define("Current")
            .Table(
                "public",
                "users",
                t =>
                    t.Column("id", PortableTypes.Uuid, c => c.PrimaryKey())
                        .Column("email", PortableTypes.VarChar(255))
                        .Index("idx_users_email", "email")
            )
            .Build();

        var desired = Schema
            .Define("Desired")
            .Table(
                "public",
                "users",
                t =>
                    t.Column("id", PortableTypes.Uuid, c => c.PrimaryKey())
                        .Column("email", PortableTypes.VarChar(255))
            )
            .Build();

        // Act
        var result = SchemaDiff.Calculate(current, desired, allowDestructive: false);

        // Assert
        Assert.True(result is OperationsResultOk);
        var ops = ((OperationsResultOk)result).Value;

        Assert.Empty(ops);
    }

    [Fact]
    public void Calculate_RemovedIndex_DroppedWhenDestructiveAllowed()
    {
        // Arrange
        var current = Schema
            .Define("Current")
            .Table(
                "public",
                "users",
                t =>
                    t.Column("id", PortableTypes.Uuid, c => c.PrimaryKey())
                        .Column("email", PortableTypes.VarChar(255))
                        .Index("idx_users_email", "email")
            )
            .Build();

        var desired = Schema
            .Define("Desired")
            .Table(
                "public",
                "users",
                t =>
                    t.Column("id", PortableTypes.Uuid, c => c.PrimaryKey())
                        .Column("email", PortableTypes.VarChar(255))
            )
            .Build();

        // Act
        var result = SchemaDiff.Calculate(current, desired, allowDestructive: true);

        // Assert
        Assert.True(result is OperationsResultOk);
        var ops = ((OperationsResultOk)result).Value;

        Assert.Single(ops);
        Assert.IsType<DropIndexOperation>(ops[0]);
        var dropIdxOp = (DropIndexOperation)ops[0];
        Assert.Equal("idx_users_email", dropIdxOp.IndexName);
    }

    [Fact]
    public void Calculate_RemovedForeignKey_DroppedWhenDestructiveAllowed()
    {
        // Arrange
        var current = Schema
            .Define("Current")
            .Table(
                "public",
                "departments",
                t => t.Column("id", PortableTypes.Uuid, c => c.PrimaryKey())
            )
            .Table(
                "public",
                "employees",
                t =>
                    t.Column("id", PortableTypes.Uuid, c => c.PrimaryKey())
                        .Column("dept_id", PortableTypes.Uuid)
                        .ForeignKey("dept_id", "departments", "id", ForeignKeyAction.Cascade)
            )
            .Build();

        var desired = Schema
            .Define("Desired")
            .Table(
                "public",
                "departments",
                t => t.Column("id", PortableTypes.Uuid, c => c.PrimaryKey())
            )
            .Table(
                "public",
                "employees",
                t =>
                    t.Column("id", PortableTypes.Uuid, c => c.PrimaryKey())
                        .Column("dept_id", PortableTypes.Uuid)
            )
            .Build();

        // Act
        var result = SchemaDiff.Calculate(current, desired, allowDestructive: true);

        // Assert
        Assert.True(result is OperationsResultOk);
        var ops = ((OperationsResultOk)result).Value;

        Assert.Single(ops);
        Assert.IsType<DropForeignKeyOperation>(ops[0]);
    }

    [Fact]
    public void Calculate_NewTableWithIndex_CreatesTableAndIndex()
    {
        // Arrange
        var current = Schema.Define("Current").Build();

        var desired = Schema
            .Define("Desired")
            .Table(
                "public",
                "users",
                t =>
                    t.Column("id", PortableTypes.Uuid, c => c.PrimaryKey())
                        .Column("email", PortableTypes.VarChar(255))
                        .Index("idx_users_email", "email")
            )
            .Build();

        // Act
        var result = SchemaDiff.Calculate(current, desired);

        // Assert
        Assert.True(result is OperationsResultOk);
        var ops = ((OperationsResultOk)result).Value;

        Assert.Equal(2, ops.Count);
        Assert.IsType<CreateTableOperation>(ops[0]);
        Assert.IsType<CreateIndexOperation>(ops[1]);
    }

    [Fact]
    public void Calculate_CaseInsensitiveTableMatching()
    {
        // Arrange
        var current = Schema
            .Define("Current")
            .Table("public", "USERS", t => t.Column("id", PortableTypes.Uuid, c => c.PrimaryKey()))
            .Build();

        var desired = Schema
            .Define("Desired")
            .Table(
                "public",
                "users",
                t =>
                    t.Column("id", PortableTypes.Uuid, c => c.PrimaryKey())
                        .Column("email", PortableTypes.VarChar(255))
            )
            .Build();

        // Act
        var result = SchemaDiff.Calculate(current, desired);

        // Assert
        Assert.True(result is OperationsResultOk);
        var ops = ((OperationsResultOk)result).Value;

        // Should recognize USERS and users as the same table, just add the column
        Assert.Single(ops);
        Assert.IsType<AddColumnOperation>(ops[0]);
    }

    [Fact]
    public void Calculate_CaseInsensitiveColumnMatching()
    {
        // Arrange
        var current = Schema
            .Define("Current")
            .Table(
                "public",
                "users",
                t =>
                    t.Column("id", PortableTypes.Uuid, c => c.PrimaryKey())
                        .Column("EMAIL", PortableTypes.VarChar(255))
            )
            .Build();

        var desired = Schema
            .Define("Desired")
            .Table(
                "public",
                "users",
                t =>
                    t.Column("id", PortableTypes.Uuid, c => c.PrimaryKey())
                        .Column("email", PortableTypes.VarChar(255))
            )
            .Build();

        // Act
        var result = SchemaDiff.Calculate(current, desired);

        // Assert
        Assert.True(result is OperationsResultOk);
        var ops = ((OperationsResultOk)result).Value;

        // Should recognize EMAIL and email as the same column
        Assert.Empty(ops);
    }

    [Fact]
    public void Calculate_MultipleNewTables_CreatesAll()
    {
        // Arrange
        var current = Schema.Define("Current").Build();

        var desired = Schema
            .Define("Desired")
            .Table(
                "public",
                "countries",
                t => t.Column("id", PortableTypes.Uuid, c => c.PrimaryKey())
            )
            .Table(
                "public",
                "regions",
                t => t.Column("id", PortableTypes.Uuid, c => c.PrimaryKey())
            )
            .Table("public", "cities", t => t.Column("id", PortableTypes.Uuid, c => c.PrimaryKey()))
            .Build();

        // Act
        var result = SchemaDiff.Calculate(current, desired);

        // Assert
        Assert.True(result is OperationsResultOk);
        var ops = ((OperationsResultOk)result).Value;

        Assert.Equal(3, ops.Count);
        Assert.All(ops, op => Assert.IsType<CreateTableOperation>(op));
    }

    [Fact]
    public void Calculate_ComplexMigration_CombinesOperations()
    {
        // Arrange
        var current = Schema
            .Define("Current")
            .Table(
                "public",
                "users",
                t =>
                    t.Column("id", PortableTypes.Uuid, c => c.PrimaryKey())
                        .Column("old_field", PortableTypes.VarChar(100))
            )
            .Table(
                "public",
                "obsolete_table",
                t => t.Column("id", PortableTypes.Uuid, c => c.PrimaryKey())
            )
            .Build();

        var desired = Schema
            .Define("Desired")
            .Table(
                "public",
                "users",
                t =>
                    t.Column("id", PortableTypes.Uuid, c => c.PrimaryKey())
                        .Column("new_field", PortableTypes.VarChar(200))
                        .Index("idx_users_new", "new_field")
            )
            .Table(
                "public",
                "new_table",
                t =>
                    t.Column("id", PortableTypes.Uuid, c => c.PrimaryKey())
                        .Column("user_id", PortableTypes.Uuid)
                        .ForeignKey("user_id", "users", "id")
            )
            .Build();

        // Act
        var result = SchemaDiff.Calculate(current, desired, allowDestructive: true);

        // Assert
        Assert.True(result is OperationsResultOk);
        var ops = ((OperationsResultOk)result).Value;

        // Should have: add column (new_field), create index, drop column (old_field),
        // create table (new_table), drop table (obsolete_table)
        Assert.Contains(ops, op => op is AddColumnOperation add && add.Column.Name == "new_field");
        Assert.Contains(
            ops,
            op => op is CreateIndexOperation idx && idx.Index.Name == "idx_users_new"
        );
        Assert.Contains(
            ops,
            op => op is DropColumnOperation drop && drop.ColumnName == "old_field"
        );
        Assert.Contains(
            ops,
            op => op is CreateTableOperation create && create.Table.Name == "new_table"
        );
        Assert.Contains(
            ops,
            op => op is DropTableOperation dropTable && dropTable.TableName == "obsolete_table"
        );
    }

    [Fact]
    public void Calculate_DesiredForcedRls_EmitsEnableForceRls()
    {
        var current = new SchemaDefinition
        {
            Name = "Current",
            Tables = [RlsTable(new RlsPolicySetDefinition { Enabled = false })],
        };

        var desired = new SchemaDefinition
        {
            Name = "Desired",
            Tables = [RlsTable(new RlsPolicySetDefinition { Forced = true })],
        };

        var result = SchemaDiff.Calculate(current, desired);

        Assert.True(result is OperationsResultOk);
        var ops = ((OperationsResultOk)result).Value;
        Assert.Contains(ops, op => op is EnableRlsOperation);
        Assert.Contains(ops, op => op is EnableForceRlsOperation);
    }

    [Fact]
    public void Calculate_CurrentForcedRls_AllowDestructive_EmitsDisableForceRls()
    {
        var current = new SchemaDefinition
        {
            Name = "Current",
            Tables = [RlsTable(new RlsPolicySetDefinition { Forced = true })],
        };

        var desired = new SchemaDefinition
        {
            Name = "Desired",
            Tables = [RlsTable(new RlsPolicySetDefinition())],
        };

        var result = SchemaDiff.Calculate(current, desired, allowDestructive: true);

        Assert.True(result is OperationsResultOk);
        var ops = ((OperationsResultOk)result).Value;
        Assert.Contains(ops, op => op is DisableForceRlsOperation);
    }

    private static TableDefinition RlsTable(RlsPolicySetDefinition rls) =>
        new()
        {
            Schema = "public",
            Name = "documents",
            Columns =
            [
                new ColumnDefinition
                {
                    Name = "id",
                    Type = PortableTypes.Uuid,
                    IsNullable = false,
                },
            ],
            PrimaryKey = new PrimaryKeyDefinition { Columns = ["id"] },
            RowLevelSecurity = rls,
        };
}
