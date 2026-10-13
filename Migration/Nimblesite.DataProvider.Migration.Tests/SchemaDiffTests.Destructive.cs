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

    // Implements [CI-TEST-SHARED-CASE-MATRIX]: the three removal kinds share one
    // destructive-drop workflow; case names preserve the original fact labels.
    [Theory]
    [MemberData(nameof(DestructiveRemovalCases))]
    public void Calculate_Removal_DroppedWhenDestructiveAllowed(
        string scenario,
        SchemaDefinition current,
        SchemaDefinition desired,
        string removedName
    )
    {
        Assert.False(string.IsNullOrEmpty(scenario));

        var op = Assert.Single(
            SchemaDiffAssertions.Diff(current: current, desired: desired, destructive: true)
        );
        var actual = op switch
        {
            DropTableOperation drop => drop.TableName,
            DropColumnOperation drop => drop.ColumnName,
            DropIndexOperation drop => drop.IndexName,
            _ => null,
        };
        Assert.Equal(removedName, actual);
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
