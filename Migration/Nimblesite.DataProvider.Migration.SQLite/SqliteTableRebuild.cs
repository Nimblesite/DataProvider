namespace Nimblesite.DataProvider.Migration.SQLite;

/// <summary>
/// SQLite's documented table rebuild (https://www.sqlite.org/lang_altertable.html#otheralter):
/// create the new shape, copy rows, drop, rename, then restore indexes and triggers.
/// Callers must turn foreign_keys off first so DROP TABLE does not fire ON DELETE actions.
/// Implements [MIG-SQLITE-REBUILD].
/// </summary>
internal static class SqliteTableRebuild
{
    internal static string Generate(RebuildTableOperation op)
    {
        var name = op.Table.Name;
        var temp = $"{name}__rebuild";
        var columns = string.Join(", ", op.CopyColumns.Select(c => $"[{c}]"));
        var indexes = op.Table.Indexes.Select(index =>
            SqliteDdlGenerator.GenerateCreateIndex(
                new CreateIndexOperation(op.Table.Schema, name, index)
            )
        );
        // legacy_alter_table stops RENAME from re-validating views (such as RLS
        // *_secure views) that reference the table while it is briefly absent.
        return string.Join(
            ";\n",
            [
                "PRAGMA legacy_alter_table = ON",
                SqliteDdlGenerator.GenerateCreateTable(op.Table with { Name = temp, Indexes = [] }),
                $"INSERT INTO [{temp}] ({columns}) SELECT {columns} FROM [{name}]",
                $"DROP TABLE [{name}]",
                $"ALTER TABLE [{temp}] RENAME TO [{name}]",
                .. indexes,
                .. op.DependentObjectSql,
                "PRAGMA legacy_alter_table = OFF",
            ]
        );
    }
}
