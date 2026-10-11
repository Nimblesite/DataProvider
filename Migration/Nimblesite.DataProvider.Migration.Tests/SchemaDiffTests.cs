// Implements [MIG-TEST-SCHEMA-FIXTURES].
namespace Nimblesite.DataProvider.Migration.Tests;

// Implements [MIG-TEST-DIFF-SHARED].

/// <summary>
/// Tests for SchemaDiff.Calculate() method.
/// Covers: create tables, add columns, create indexes, add foreign keys, destructive operations.
/// </summary>
public sealed partial class SchemaDiffTests
{
    [Fact]
    public void Calculate_EmptyCurrentToNewDesired_CreatesTable()
    {
        // Arrange
        var current = Schema.Define("Current").Build();

        var desired = Fixture_Calculate_EmptyCurrentToNewDesired_CreatesTable_1();

        // Act
        var createOp = VerifyReusableWorkflow9<CreateTableOperation>(
            input0: current,
            input1: desired
        );
        Assert.Equal("users", createOp.Table.Name);
    }

    [Fact]
    public void Calculate_SameSchema_NoOperations()
    {
        // Arrange
        var schema = Fixture_Calculate_SameSchema_NoOperations_1();

        // Act
        var ops = SchemaDiffAssertions.Diff(current: schema, desired: schema);

        Assert.Empty(ops);
    }

    [Fact]
    public void Calculate_NewColumn_AddsColumn()
    {
        // Arrange
        var current = UsersSchema(name: "Current");

        var desired = Fixture_Calculate_NewColumn_AddsColumn_1();

        // Act
        var addColOp = VerifyReusableWorkflow9<AddColumnOperation>(
            input0: current,
            input1: desired
        );
        Assert.Equal("email", addColOp.Column.Name);
        Assert.Equal("users", addColOp.TableName);
    }

    [Fact]
    public void Calculate_NewIndex_CreatesIndex()
    {
        // Arrange
        var current = UsersSchema(name: "Current", email: true);

        var desired = UsersSchema(name: "Desired", indexEmail: true);

        // Act
        var createIdxOp = VerifyReusableWorkflow9<CreateIndexOperation>(
            input0: current,
            input1: desired
        );
        Assert.Equal("idx_users_email", createIdxOp.Index.Name);
    }

    [Fact]
    public void Calculate_NewForeignKey_AddsForeignKey()
    {
        // Arrange
        var current = Fixture_Calculate_NewForeignKey_AddsForeignKey_1();

        var desired = Fixture_Calculate_NewForeignKey_AddsForeignKey_2();

        // Act
        var addFkOp = VerifyReusableWorkflow9<AddForeignKeyOperation>(
            input0: current,
            input1: desired
        );
        Assert.Equal("employees", addFkOp.TableName);
    }

    [Fact]
    public void Calculate_NewTableWithIndex_CreatesTableAndIndex()
    {
        // Arrange
        var current = Schema.Define("Current").Build();

        var desired = UsersSchema(name: "Desired", indexEmail: true);

        // Act
        var ops = SchemaDiffAssertions.Diff(current: current, desired: desired);

        Assert.Equal(2, ops.Count);
        Assert.IsType<CreateTableOperation>(ops[0]);
        Assert.IsType<CreateIndexOperation>(ops[1]);
    }

    [Fact]
    public void Calculate_CaseInsensitiveTableMatching()
    {
        // Arrange
        var current = Data_Calculate_CaseInsensitiveTableMatching_1();

        var desired = UsersSchema(name: "Desired", email: true);

        // Act
        _ = VerifyReusableWorkflow9<AddColumnOperation>(input0: current, input1: desired);
    }

    [Fact]
    public void Calculate_CaseInsensitiveColumnMatching()
    {
        // Arrange
        var current = Fixture_Calculate_CaseInsensitiveColumnMatching_1();

        var desired = UsersSchema(name: "Desired", email: true);

        // Act
        var ops = SchemaDiffAssertions.Diff(current: current, desired: desired);

        // Should recognize EMAIL and email as the same column
        Assert.Empty(ops);
    }

    [Fact]
    public void Calculate_MultipleNewTables_CreatesAll()
    {
        // Arrange
        var current = Schema.Define("Current").Build();

        var desired = Fixture_Calculate_MultipleNewTables_CreatesAll_1();

        // Act
        var ops = SchemaDiffAssertions.Diff(current: current, desired: desired);

        Assert.Equal(3, ops.Count);
        Assert.All(ops, op => Assert.IsType<CreateTableOperation>(op));
    }

    [Fact]
    public void Calculate_ComplexMigration_CombinesOperations()
    {
        // Arrange
        var current = Fixture_Calculate_ComplexMigration_CombinesOperations_1();

        var desired = Fixture_Calculate_ComplexMigration_CombinesOperations_2();

        // Act
        var ops = SchemaDiffAssertions.Diff(current: current, desired: desired, destructive: true);

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
        var current = RlsSchema("Current", new RlsPolicySetDefinition { Enabled = false });

        var desired = RlsSchema("Desired", new RlsPolicySetDefinition { Forced = true });

        var ops = SchemaDiffAssertions.Diff(current: current, desired: desired);
        Assert.Contains(ops, op => op is EnableRlsOperation);
        Assert.Contains(ops, op => op is EnableForceRlsOperation);
    }

    // Implements [MIG-TEST-USER-FIXTURE].
    private static SchemaDefinition UsersSchema(
        string name,
        bool email = false,
        bool indexEmail = false,
        bool oldField = false
    ) =>
        Schema
            .Define(name)
            .Table(
                schema: "public",
                name: "users",
                configure: table =>
                    ConfigureUsers(
                        table: table,
                        email: email,
                        indexEmail: indexEmail,
                        oldField: oldField
                    )
            )
            .Build();

    private static void ConfigureUsers(
        TableBuilder table,
        bool email,
        bool indexEmail,
        bool oldField
    )
    {
        table.Column(name: "id", type: PortableTypes.Uuid, configure: c => c.PrimaryKey());
        if (oldField)
            table.Column(name: "old_field", type: PortableTypes.VarChar(100));
        if (email || indexEmail)
            table.Column(name: "email", type: PortableTypes.VarChar(255));
        if (indexEmail)
            table.Index(name: "idx_users_email", column: "email");
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private static SchemaDefinition RlsSchema(string name, RlsPolicySetDefinition rls) =>
        SchemaTestData.Schema(name: name, tables: [RlsTable(rls)]);

    // Implements [CI-TEST-SHARED-SCENARIOS].
    internal static T VerifyReusableWorkflow9<T>(SchemaDefinition input0, SchemaDefinition input1)
        where T : SchemaOperation =>
        Assert.IsType<T>(Assert.Single(VerifyReusableWorkflow9(input0, input1)));

    // Implements [CI-TEST-SHARED-SCENARIOS].
    internal static IReadOnlyList<SchemaOperation> VerifyReusableWorkflow9(
        SchemaDefinition input0,
        SchemaDefinition input1
    )
    {
        // Act
        var ops = SchemaDiffAssertions.Diff(current: input0, desired: input1);

        Assert.Single(ops);

        return ops;
    }
}
