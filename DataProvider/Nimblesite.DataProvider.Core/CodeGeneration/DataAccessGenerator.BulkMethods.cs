using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using Nimblesite.Sql.Model;
using BulkSourceError = Outcome.Result<string, Nimblesite.Sql.Model.SqlError>.Error<
    string,
    Nimblesite.Sql.Model.SqlError
>;
using BulkSourceOk = Outcome.Result<string, Nimblesite.Sql.Model.SqlError>.Ok<
    string,
    Nimblesite.Sql.Model.SqlError
>;
using BulkSourceResult = Outcome.Result<string, Nimblesite.Sql.Model.SqlError>;

namespace Nimblesite.DataProvider.Core.CodeGeneration;

// Implements [DP-CODEGEN-SHARED-BULK-METHODS].
public static partial class DataAccessGenerator
{
    private static BulkSourceResult GenerateBulkMethod(
        DatabaseTable table,
        string databaseType,
        int batchSize,
        string connectionType,
        bool upsert
    )
    {
        if (table is null)
            return new BulkSourceError(new SqlError(Message: "table cannot be null"));
        if (table.InsertableColumns.Count == 0 || upsert && table.PrimaryKeyColumns.Count == 0)
            return new BulkSourceOk("");
        var columns = BulkColumns(table: table, upsert: upsert);
        var tupleType = string.Join(
            ", ",
            columns.Select(c =>
                string.Create(CultureInfo.InvariantCulture, $"{c.CSharpType} {c.Name}")
            )
        );
        var builder = new StringBuilder();
        AppendBulkMainHeader(
            builder: builder,
            tableName: table.Name,
            tupleType: tupleType,
            upsert: upsert
        );
        AppendBulkBatchLoop(
            builder: builder,
            tableName: table.Name,
            tupleType: tupleType,
            batchSize: batchSize,
            operation: upsert ? "Upsert" : "Insert",
            totalVariable: upsert ? "totalAffected" : "totalInserted",
            failure: upsert ? "Bulk upsert failed" : "Bulk insert failed"
        );
        AppendBulkBatchHeader(
            builder: builder,
            tableName: table.Name,
            tupleType: tupleType,
            upsert: upsert
        );
        AppendBulkValues(
            builder: builder,
            table: table,
            columns: columns,
            replace: upsert && !databaseType.Equals("Postgres", StringComparison.OrdinalIgnoreCase)
        );
        if (upsert && databaseType.Equals("Postgres", StringComparison.OrdinalIgnoreCase))
            AppendBulkConflict(builder: builder, table: table, columns: columns);
        AppendBatchCommandExecute(sb: builder, connectionType: connectionType);
        builder.AppendLine("    }");
        return new BulkSourceOk(builder.ToString());
    }

    private static IReadOnlyList<DatabaseColumn> BulkColumns(DatabaseTable table, bool upsert) =>
        upsert
            ? table
                .PrimaryKeyColumns.Concat(
                    table.InsertableColumns.Where(c =>
                        !table.PrimaryKeyColumns.Any(pk => pk.Name == c.Name)
                    )
                )
                .ToImmutableArray()
            : table.InsertableColumns;

    private static void AppendBulkMainHeader(
        StringBuilder builder,
        string tableName,
        string tupleType,
        bool upsert
    )
    {
        var operation = upsert ? "Upsert" : "Insert";
        var action = upsert ? "upsert" : "insert";
        ImmutableArray<string> lines =
        [
            "",
            "    /// <summary>",
            upsert
                ? $"    /// Bulk upserts rows into the {tableName} table (insert or update on conflict)."
                : $"    /// Bulk inserts rows into the {tableName} table using batched multi-row VALUES.",
            "    /// </summary>",
            "    /// <param name=\"transaction\">Active database transaction.</param>",
            $"    /// <param name=\"records\">Records to {action}.</param>",
            $"    /// <returns>Result with total rows {(upsert ? "affected" : "inserted")} or SQL error.</returns>",
            $"    public static async Task<Result<int, SqlError>> Bulk{operation}{tableName}Async(this IDbTransaction transaction, IEnumerable<({tupleType})> records)",
        ];
        foreach (var line in lines)
            builder.AppendLine(line);
    }

    private static void AppendBulkBatchHeader(
        StringBuilder builder,
        string tableName,
        string tupleType,
        bool upsert
    )
    {
        ImmutableArray<string> lines =
        [
            "    /// <summary>",
            $"    /// Executes a single batch of bulk {(upsert ? "upserts" : "inserts")} for {tableName}.",
            "    /// </summary>",
            $"    private static async Task<Result<int, SqlError>> ExecuteBulk{(upsert ? "Upsert" : "Insert")}{tableName}BatchAsync(IDbTransaction transaction, List<({tupleType})> batch)",
            "    {",
            "        if (batch.Count == 0)",
            "            return new Result<int, SqlError>.Ok<int, SqlError>(0);",
        ];
        foreach (var line in lines)
            builder.AppendLine(line);
        AppendTransactionConnectionGuard(sb: builder);
    }

    private static void AppendBulkValues(
        StringBuilder builder,
        DatabaseTable table,
        IReadOnlyList<DatabaseColumn> columns,
        bool replace
    )
    {
        var columnNames = string.Join(", ", columns.Select(c => c.Name));
        var placeholders = string.Join(
            ", ",
            columns.Select((c, index) => $"@p\" + (i * {columns.Count} + {index}) + \"")
        );
        ImmutableArray<string> lines =
        [
            $"        var sql = new StringBuilder(\"{(replace ? "INSERT OR REPLACE" : "INSERT")} INTO {table.Name} ({columnNames}) VALUES \");",
            "        var parameters = new List<object?>();",
            "",
            "        for (int i = 0; i < batch.Count; i++)",
            "        {",
            "            if (i > 0) sql.Append(\", \");",
            $"            sql.Append(\"({placeholders})\");",
            "            var rec = batch[i];",
        ];
        foreach (var line in lines)
            builder.AppendLine(line);
        foreach (var column in columns)
            builder.AppendLine(
                CultureInfo.InvariantCulture,
                $"            parameters.Add(rec.{column.Name});"
            );
        builder.AppendLine("        }");
    }

    private static void AppendBulkConflict(
        StringBuilder builder,
        DatabaseTable table,
        IReadOnlyList<DatabaseColumn> columns
    )
    {
        var updateColumns = columns
            .Where(c => !table.PrimaryKeyColumns.Any(pk => pk.Name == c.Name))
            .ToImmutableArray();
        if (updateColumns.IsEmpty)
            return;
        var primaryKeys = string.Join(", ", table.PrimaryKeyColumns.Select(c => c.Name));
        var updateSet = string.Join(
            ", ",
            updateColumns.Select(c => $"{c.Name} = EXCLUDED.{c.Name}")
        );
        builder.AppendLine();
        builder.AppendLine(
            CultureInfo.InvariantCulture,
            $"        sql.Append(\" ON CONFLICT ({primaryKeys}) DO UPDATE SET {updateSet}\");"
        );
    }
}
