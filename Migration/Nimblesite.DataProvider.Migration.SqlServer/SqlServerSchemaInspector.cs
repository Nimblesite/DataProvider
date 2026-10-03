using System.Collections.Immutable;

namespace Nimblesite.DataProvider.Migration.SqlServer;

/// <summary>
/// Inspects a SQL Server database and returns a SchemaDefinition covering
/// tables, columns, primary keys, unique constraints, foreign keys, indexes
/// and check constraints. Implements [MIG-SQLSERVER].
/// </summary>
public static class SqlServerSchemaInspector
{
    /// <summary>
    /// Inspect every user table in the database.
    /// </summary>
    /// <param name="connection">Open SQL Server connection</param>
    /// <param name="logger">Optional logger</param>
    public static SchemaResult Inspect(SqlConnection connection, ILogger? logger = null)
    {
        try
        {
            logger?.LogDebug("Inspecting SQL Server schema");
            var catalog = new Catalog(
                Columns: SqlServerCatalogQueries.Columns(connection),
                Keys: SqlServerCatalogQueries.Keys(connection),
                ForeignKeys: SqlServerCatalogQueries.ForeignKeys(connection),
                Indexes: SqlServerCatalogQueries.Indexes(connection),
                Checks: SqlServerCatalogQueries.Checks(connection)
            );
            var tables = SqlServerCatalogQueries
                .Tables(connection)
                .Select(t => BuildTable(t.Schema, t.Table, catalog))
                .ToList();
            logger?.LogDebug("Inspected {TableCount} SQL Server tables", tables.Count);
            return new SchemaResult.Ok<SchemaDefinition, MigrationError>(
                new SchemaDefinition { Name = "sqlserver", Tables = tables.AsReadOnly() }
            );
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to inspect SQL Server schema");
            return new SchemaResult.Error<SchemaDefinition, MigrationError>(
                MigrationError.FromException(ex)
            );
        }
    }

    /// <summary>
    /// Map portable default schema names (public, main, empty) to SQL Server's dbo.
    /// </summary>
    internal static string EffectiveSchema(string schema) =>
        string.IsNullOrWhiteSpace(schema)
        || schema.Equals("public", StringComparison.OrdinalIgnoreCase)
        || schema.Equals("main", StringComparison.OrdinalIgnoreCase)
            ? "dbo"
            : schema;

    private sealed record Catalog(
        ImmutableArray<ColumnRow> Columns,
        ImmutableArray<KeyRow> Keys,
        ImmutableArray<ForeignKeyRow> ForeignKeys,
        ImmutableArray<IndexRow> Indexes,
        ImmutableArray<CheckRow> Checks
    );

    private static TableDefinition BuildTable(string schema, string table, Catalog catalog)
    {
        bool Owns(string s, string t) => s == schema && t == table;
        var keys = catalog.Keys.Where(k => Owns(k.Schema, k.Table)).ToList();
        var checks = catalog.Checks.Where(c => Owns(c.Schema, c.Table)).ToList();
        return new TableDefinition
        {
            Schema = schema,
            Name = table,
            Columns = BuildColumns(catalog.Columns.Where(c => Owns(c.Schema, c.Table)), checks),
            PrimaryKey = BuildPrimaryKey(keys),
            UniqueConstraints = BuildUniqueConstraints(keys),
            ForeignKeys = BuildForeignKeys(catalog.ForeignKeys.Where(f => Owns(f.Schema, f.Table))),
            Indexes = BuildIndexes(catalog.Indexes.Where(i => Owns(i.Schema, i.Table))),
            CheckConstraints = checks
                .Where(c => c.Column is null)
                .Select(c => new CheckConstraintDefinition
                {
                    Name = c.Name,
                    Expression = c.Expression,
                })
                .ToList(),
        };
    }

    private static List<ColumnDefinition> BuildColumns(
        IEnumerable<ColumnRow> rows,
        List<CheckRow> checks
    ) =>
        rows.Select(row =>
                checks.FirstOrDefault(c => c.Column == row.Column.Name) is { } check
                    ? row.Column with
                    {
                        CheckConstraint = check.Expression,
                        CheckConstraintName = check.Name,
                    }
                    : row.Column
            )
            .ToList();

    private static PrimaryKeyDefinition? BuildPrimaryKey(List<KeyRow> keys) =>
        keys.Where(k => k.IsPrimary).ToList() is { Count: > 0 } pk
            ? new PrimaryKeyDefinition
            {
                Name = pk[0].Name,
                Columns = pk.Select(k => k.Column).ToList(),
            }
            : null;

    private static List<UniqueConstraintDefinition> BuildUniqueConstraints(List<KeyRow> keys) =>
        keys.Where(k => !k.IsPrimary)
            .GroupBy(k => k.Name)
            .Select(g => new UniqueConstraintDefinition
            {
                Name = g.Key,
                Columns = g.Select(k => k.Column).ToList(),
            })
            .ToList();

    private static List<ForeignKeyDefinition> BuildForeignKeys(IEnumerable<ForeignKeyRow> rows) =>
        rows.GroupBy(f => f.Name)
            .Select(g => new ForeignKeyDefinition
            {
                Name = g.Key,
                Columns = g.Select(f => f.Column).ToList(),
                ReferencedSchema = g.First().ReferencedSchema,
                ReferencedTable = g.First().ReferencedTable,
                ReferencedColumns = g.Select(f => f.ReferencedColumn).ToList(),
                OnDelete = g.First().OnDelete,
                OnUpdate = g.First().OnUpdate,
            })
            .ToList();

    private static List<IndexDefinition> BuildIndexes(IEnumerable<IndexRow> rows) =>
        rows.GroupBy(i => i.Name)
            .Select(g => new IndexDefinition
            {
                Name = g.Key,
                IsUnique = g.First().IsUnique,
                Filter = g.First().Filter,
                Columns = g.Select(i => i.Column).ToList(),
            })
            .ToList();
}
