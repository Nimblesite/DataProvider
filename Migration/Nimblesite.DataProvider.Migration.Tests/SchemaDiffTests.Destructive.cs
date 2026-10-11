// Implements [MIG-TEST-SCHEMA-FIXTURES].
namespace Nimblesite.DataProvider.Migration.Tests;

// Implements [MIG-TEST-DIFF-SHARED].
public sealed partial class SchemaDiffTests
{
    // Implements [MIG-TEST-DIFF-SHARED].
    [Theory]
    [MemberData(nameof(NonDestructiveRemovalCases))]
    public void Calculate_Removal_NotDroppedByDefault(
        string scenario,
        SchemaDefinition current,
        SchemaDefinition desired
    )
    {
        _ = scenario;
        var ops = SchemaDiffAssertions.Diff(current: current, desired: desired, destructive: false);
        Assert.Empty(collection: ops);
    }

    [Fact]
    public void Calculate_RemovedTable_DroppedWhenDestructiveAllowed()
    {
        // Arrange
        var current = Fixture_Calculate_RemovedTable_DroppedWhenDestructiveAllowed_1();

        var desired = UsersSchema(name: "Desired");

        // Act
        var dropOp = SchemaDiffAssertions.DiffSingle<DropTableOperation>(
            current: current,
            desired: desired,
            destructive: true
        );
        Assert.Equal("obsolete", dropOp.TableName);
    }

    [Fact]
    public void Calculate_RemovedColumn_DroppedWhenDestructiveAllowed()
    {
        // Arrange
        var current = UsersSchema(name: "Current", oldField: true);

        var desired = UsersSchema(name: "Desired");

        // Act
        var dropColOp = SchemaDiffAssertions.DiffSingle<DropColumnOperation>(
            current: current,
            desired: desired,
            destructive: true
        );
        Assert.Equal("old_field", dropColOp.ColumnName);
    }

    [Fact]
    public void Calculate_RemovedIndex_DroppedWhenDestructiveAllowed()
    {
        // Arrange
        var current = UsersSchema(name: "Current", indexEmail: true);

        var desired = UsersSchema(name: "Desired", email: true);

        // Act
        var dropIdxOp = SchemaDiffAssertions.DiffSingle<DropIndexOperation>(
            current: current,
            desired: desired,
            destructive: true
        );
        Assert.Equal("idx_users_email", dropIdxOp.IndexName);
    }

    [Fact]
    public void Calculate_RemovedForeignKey_DroppedWhenDestructiveAllowed()
    {
        // Arrange
        var current = Fixture_Calculate_RemovedForeignKey_DroppedWhenDestructiveAllowed_1();

        var desired = Fixture_Calculate_RemovedForeignKey_DroppedWhenDestructiveAllowed_2();

        // Act
        _ = SchemaDiffAssertions.DiffSingle<DropForeignKeyOperation>(
            current: current,
            desired: desired,
            destructive: true
        );
    }

    [Fact]
    public void Calculate_CurrentForcedRls_AllowDestructive_EmitsDisableForceRls()
    {
        var current = RlsSchema("Current", new RlsPolicySetDefinition { Forced = true });

        var desired = RlsSchema("Desired", new RlsPolicySetDefinition());

        var ops = SchemaDiffAssertions.Diff(current: current, desired: desired, destructive: true);
        Assert.Contains(ops, op => op is DisableForceRlsOperation);
    }
}
