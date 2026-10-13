namespace Nimblesite.DataProvider.Migration.Core;

/// <summary>
/// Shared schema-inspection workflow used by every relational provider. Reads a
/// provider-specific table list, inspects each table in turn, collects the
/// successes and logs per-table failures without aborting the whole schema.
/// Implements [MIG-INSPECT-SHARED-TABLES].
/// </summary>
internal static class SchemaInspectionSupport
{
    /// <summary>
    /// Inspects each table name and adds successful definitions to
    /// <paramref name="tables" />, logging a warning for each failure.
    /// </summary>
    internal static void CollectTables(
        IEnumerable<string> tableNames,
        Func<string, Outcome.Result<TableDefinition, MigrationError>> inspectTable,
        ICollection<TableDefinition> tables,
        ILogger? logger
    )
    {
        foreach (var tableName in tableNames)
        {
            var tableResult = inspectTable(tableName);
            if (
                tableResult
                is Outcome.Result<TableDefinition, MigrationError>.Ok<
                    TableDefinition,
                    MigrationError
                > ok
            )
            {
                tables.Add(ok.Value);
            }
            else if (
                tableResult
                is Outcome.Result<TableDefinition, MigrationError>.Error<
                    TableDefinition,
                    MigrationError
                > tableError
            )
            {
                logger?.LogWarning(
                    "Failed to inspect table {Table}: {Error}",
                    tableName,
                    tableError.Value
                );
            }
        }
    }
}
