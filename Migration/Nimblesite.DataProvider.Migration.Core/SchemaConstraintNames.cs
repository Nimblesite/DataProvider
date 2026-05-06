namespace Nimblesite.DataProvider.Migration.Core;

/// <summary>
/// Stable constraint name helpers shared by diff and platform DDL generators.
/// </summary>
public static class SchemaConstraintNames
{
    /// <summary>
    /// Resolve the database name for a column-level check constraint.
    /// Implements [MIG-PG-NAMED-COLUMN-CHECK-CONSTRAINT].
    /// </summary>
    public static string ColumnCheck(string tableName, ColumnDefinition column)
    {
        var name = column.CheckConstraintName;
        if (!string.IsNullOrWhiteSpace(name))
        {
            return name;
        }

        return $"{tableName}_{column.Name}_chk";
    }
}
