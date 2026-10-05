namespace Nimblesite.DataProvider.Migration.Tests;

// Implements [MIG-TEST-DIFF-SHARED].
public sealed partial class SchemaDiffTests
{
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

        var desired = UsersSchema(name: "Desired");

        // Act
        var ops = SchemaDiffAssertions.Diff(current: current, desired: desired, destructive: false);

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

        var desired = UsersSchema(name: "Desired");

        // Act
        var ops = SchemaDiffAssertions.Diff(current: current, desired: desired, destructive: true);

        Assert.Single(ops);
        Assert.IsType<DropTableOperation>(ops[0]);
        var dropOp = (DropTableOperation)ops[0];
        Assert.Equal("obsolete", dropOp.TableName);
    }

    [Fact]
    public void Calculate_RemovedColumn_NotDroppedByDefault()
    {
        // Arrange
        var current = UsersSchema(name: "Current", oldField: true);

        var desired = UsersSchema(name: "Desired");

        // Act
        var ops = SchemaDiffAssertions.Diff(current: current, desired: desired, destructive: false);

        Assert.Empty(ops);
    }

    [Fact]
    public void Calculate_RemovedColumn_DroppedWhenDestructiveAllowed()
    {
        // Arrange
        var current = UsersSchema(name: "Current", oldField: true);

        var desired = UsersSchema(name: "Desired");

        // Act
        var ops = SchemaDiffAssertions.Diff(current: current, desired: desired, destructive: true);

        Assert.Single(ops);
        Assert.IsType<DropColumnOperation>(ops[0]);
        var dropColOp = (DropColumnOperation)ops[0];
        Assert.Equal("old_field", dropColOp.ColumnName);
    }

    [Fact]
    public void Calculate_RemovedIndex_NotDroppedByDefault()
    {
        // Arrange
        var current = UsersSchema(name: "Current", indexEmail: true);

        var desired = UsersSchema(name: "Desired", email: true);

        // Act
        var ops = SchemaDiffAssertions.Diff(current: current, desired: desired, destructive: false);

        Assert.Empty(ops);
    }

    [Fact]
    public void Calculate_RemovedIndex_DroppedWhenDestructiveAllowed()
    {
        // Arrange
        var current = UsersSchema(name: "Current", indexEmail: true);

        var desired = UsersSchema(name: "Desired", email: true);

        // Act
        var ops = SchemaDiffAssertions.Diff(current: current, desired: desired, destructive: true);

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
        var ops = SchemaDiffAssertions.Diff(current: current, desired: desired, destructive: true);

        Assert.Single(ops);
        Assert.IsType<DropForeignKeyOperation>(ops[0]);
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

        var ops = SchemaDiffAssertions.Diff(current: current, desired: desired, destructive: true);
        Assert.Contains(ops, op => op is DisableForceRlsOperation);
    }
}
