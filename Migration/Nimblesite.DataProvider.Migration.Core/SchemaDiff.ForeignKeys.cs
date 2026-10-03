namespace Nimblesite.DataProvider.Migration.Core;

public static partial class SchemaDiff
{
    // Implements [MIG-EXISTING-DATABASE-UPGRADE]: unnamed YAML foreign keys
    // are identified by their relationship, including on destructive reruns.
    private static bool ForeignKeysMatch(
        ForeignKeyDefinition current,
        ForeignKeyDefinition desired
    ) =>
        (
            string.IsNullOrWhiteSpace(desired.Name)
            || string.IsNullOrWhiteSpace(current.Name)
            || string.Equals(current.Name, desired.Name, StringComparison.OrdinalIgnoreCase)
        )
        && string.Equals(
            current.ReferencedSchema,
            desired.ReferencedSchema,
            StringComparison.OrdinalIgnoreCase
        )
        && ForeignKeyRelationshipsMatch(current: current, desired: desired);

    // SQLite catalog names and schema aliases cannot identify constraints.
    // Reuse the relationship comparison when planning a SQLite table rebuild.
    internal static bool ForeignKeyRelationshipsMatch(
        ForeignKeyDefinition current,
        ForeignKeyDefinition desired
    ) =>
        current.Columns.SequenceEqual(desired.Columns, StringComparer.OrdinalIgnoreCase)
        && string.Equals(
            current.ReferencedTable,
            desired.ReferencedTable,
            StringComparison.OrdinalIgnoreCase
        )
        && current.ReferencedColumns.SequenceEqual(
            desired.ReferencedColumns,
            StringComparer.OrdinalIgnoreCase
        )
        && current.OnDelete == desired.OnDelete
        && current.OnUpdate == desired.OnUpdate;
}
