namespace Nimblesite.DataProvider.Migration.SqlServer;

/// <summary>
/// Compares SQL Server declarations with the types its catalog can report.
/// Implements [MIG-SQLSERVER].
/// </summary>
public static class SqlServerSchemaNormalizer
{
    /// <summary>Normalize portable types that share one SQL Server storage type.</summary>
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
        column.Type is JsonType or NVarCharType { MaxLength: int.MaxValue }
            ? column with
            {
                Type = new TextType(),
            }
            : column;
}
