namespace Nimblesite.DataProvider.Migration.SQLite;

/// <summary>
/// Rewrites a desired schema into the column types SQLite actually stores so the
/// integrity verifier compares like with like (for example UUID is stored as TEXT).
/// Implements [MIG-VERIFY-BEFORE-UP-TO-DATE].
/// </summary>
public static class SqliteSchemaNormalizer
{
    /// <summary>
    /// Map every desired column type to its SQLite round-trip type.
    /// </summary>
    /// <param name="desired">The declared schema</param>
    public static SchemaResult Normalize(SchemaDefinition desired) =>
        new SchemaResult.Ok<SchemaDefinition, MigrationError>(
            desired with
            {
                Tables = desired
                    .Tables.Select(table =>
                        table with
                        {
                            Columns = table.Columns.Select(RoundTrip).ToList(),
                        }
                    )
                    .ToList(),
            }
        );

    private static ColumnDefinition RoundTrip(ColumnDefinition column) =>
        column with
        {
            Type = SqliteSchemaInspector.SqliteTypeToPortable(
                SqliteDdlGenerator.PortableTypeToSqlite(column.Type)
            ),
        };
}
