namespace Nimblesite.DataProvider.Migration.Core;

/// <summary>
/// Primary key columns are NOT NULL on PostgreSQL and SQL Server whatever the schema
/// declares, while SQLite allows NULL in non-integer keys. A key column left at the
/// default nullability therefore takes the platform's behaviour: it is never relaxed
/// and never reported as drift. Implements [MIG-VERIFY-BEFORE-UP-TO-DATE].
/// </summary>
public static class PrimaryKeyNullability
{
    /// <summary>
    /// True when <paramref name="column"/> is a primary key column left at the default nullability.
    /// </summary>
    public static bool UsesPlatformDefault(TableDefinition table, ColumnDefinition column) =>
        column.IsNullable
        && (
            table.PrimaryKey?.Columns.Any(key =>
                string.Equals(key, column.Name, StringComparison.OrdinalIgnoreCase)
            )
            ?? false
        );
}
