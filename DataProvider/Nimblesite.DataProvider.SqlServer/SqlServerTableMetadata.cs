using System.Collections.Immutable;
using Nimblesite.DataProvider.Core;
using ColumnsOk = Outcome.Result<
    System.Collections.Generic.IReadOnlyList<Nimblesite.DataProvider.Core.DatabaseColumn>,
    Nimblesite.Sql.Model.SqlError
>.Ok<
    System.Collections.Generic.IReadOnlyList<Nimblesite.DataProvider.Core.DatabaseColumn>,
    Nimblesite.Sql.Model.SqlError
>;
using SqlError = Nimblesite.Sql.Model.SqlError;
using TableResult = Outcome.Result<
    Nimblesite.DataProvider.Core.DatabaseTable,
    Nimblesite.Sql.Model.SqlError
>;

namespace Nimblesite.DataProvider.SqlServer;

/// <summary>Reads live SQL Server table metadata for the shared CRUD generator. Implements [CON-SHARED-CORE].</summary>
public static class SqlServerTableMetadata
{
    /// <summary>Discovers column types, primary keys, identity and computed columns.</summary>
    public static async Task<TableResult> ReadAsync(string connectionString, TableConfig table)
    {
        var schema = string.IsNullOrWhiteSpace(table.Schema) ? "dbo" : table.Schema;
        var sql = $"SELECT * FROM {Quote(schema)}.{Quote(table.Name)}";
        var columns = await SqlServerCodeGenerator
            .CreateConfig()
            .GetColumnMetadata(connectionString, sql, [])
            .ConfigureAwait(false);
        return columns is ColumnsOk success
            ? new TableResult.Ok<DatabaseTable, SqlError>(
                new DatabaseTable
                {
                    Schema = schema,
                    Name = table.Name,
                    Columns = success
                        .Value.Select(column =>
                            table.PrimaryKeyColumns.Count == 0
                                ? column
                                : column with
                                {
                                    IsPrimaryKey = table.PrimaryKeyColumns.Contains(
                                        column.Name,
                                        StringComparer.OrdinalIgnoreCase
                                    ),
                                }
                        )
                        .ToImmutableArray(),
                }
            )
            : columns.Match<TableResult>(
                _ => new TableResult.Error<DatabaseTable, SqlError>(new SqlError("No columns")),
                error => new TableResult.Error<DatabaseTable, SqlError>(error)
            );
    }

    private static string Quote(string identifier) =>
        $"[{identifier.Replace("]", "]]", StringComparison.Ordinal)}]";
}
