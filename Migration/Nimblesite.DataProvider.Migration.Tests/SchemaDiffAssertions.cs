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
}
