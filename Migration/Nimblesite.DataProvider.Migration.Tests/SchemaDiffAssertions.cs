namespace Nimblesite.DataProvider.Migration.Tests;

// Implements [MIG-TEST-DIFF-SHARED].
internal static class SchemaDiffAssertions
{
    internal static IReadOnlyList<SchemaOperation> Diff(
        SchemaDefinition current,
        SchemaDefinition desired,
        bool destructive = false,
        ILogger? logger = null
    ) =>
        Assert
            .IsType<OperationsResultOk>(
                SchemaDiff.Calculate(
                    current: current,
                    desired: desired,
                    allowDestructive: destructive,
                    logger: logger
                )
            )
            .Value;

    // Implements [MIG-TEST-DIFF-SHARED].
    internal static T DiffSingle<T>(
        SchemaDefinition current,
        SchemaDefinition desired,
        bool destructive = false,
        ILogger? logger = null
    )
        where T : SchemaOperation =>
        Assert.IsType<T>(Assert.Single(Diff(current, desired, destructive, logger)));

    // Implements [MIG-TEST-SHARED-INDEX-CONVERSION]: a column/expression index
    // conversion is exactly one drop followed by one create.
    internal static void AssertDropAndCreateIndex(IReadOnlyList<SchemaOperation> upgradeOps)
    {
        Assert.Equal(2, upgradeOps.Count);
        Assert.Contains(upgradeOps, op => op is DropIndexOperation);
        Assert.Contains(upgradeOps, op => op is CreateIndexOperation);
    }

    // Implements [MIG-TEST-SHARED-OPERATION-ASSERTIONS]: one place for the
    // "diff contains a matching typed operation" check so the typed pattern
    // match and predicate stay identical across every diff test.
    internal static void AssertContainsOperation<T>(
        IEnumerable<SchemaOperation> operations,
        Func<T, bool> match
    )
        where T : SchemaOperation =>
        Assert.Contains(operations, op => op is T typed && match(typed));

    // Implements [MIG-TEST-SHARED-OPERATION-ASSERTIONS].
    internal static void AssertDropsRlsPolicy(
        IEnumerable<SchemaOperation> operations,
        string policyName
    ) =>
        AssertContainsOperation<DropRlsPolicyOperation>(
            operations,
            drop => drop.PolicyName == policyName
        );
}
