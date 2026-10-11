using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Nimblesite.DataProvider.Core;
using Nimblesite.DataProvider.Migration.Core;
using Nimblesite.DataProvider.Migration.SqlServer;
using Nimblesite.Sync.Core;
using BoolResult = Outcome.Result<bool, Nimblesite.Sync.Core.SyncError>;
using SchemaOk = Outcome.Result<
    Nimblesite.DataProvider.Migration.Core.SchemaDefinition,
    Nimblesite.DataProvider.Migration.Core.MigrationError
>.Ok<
    Nimblesite.DataProvider.Migration.Core.SchemaDefinition,
    Nimblesite.DataProvider.Migration.Core.MigrationError
>;

namespace Nimblesite.Sync.SqlServer;

/// <summary>Generates set-based SQL Server change tracking triggers. Implements [SYNC-TRIGGER-SHARED-GENERATION].</summary>
public static class SqlServerTriggerGenerator
{
    /// <summary>Creates insert, update and delete triggers from live schema metadata.</summary>
    public static BoolResult CreateTriggers(
        SqlConnection connection,
        string tableName,
        ILogger logger
    )
    {
        var schema = SqlServerSchemaInspector.Inspect(connection: connection, logger: logger);
        var table = schema is SchemaOk inspected
            ? inspected.Value.Tables.SingleOrDefault(candidate =>
                candidate.Name == tableName || $"{candidate.Schema}.{candidate.Name}" == tableName
            )
            : null;
        if (table is not { PrimaryKey.Columns.Count: 1 })
            return new BoolResult.Error<bool, SyncError>(
                new SyncErrorDatabase("Sync requires a table with one primary key")
            );
        foreach (var operation in new[] { "insert", "update", "delete" })
        {
            var result = connection.Execute(sql: Generate(table: table, operation: operation));
            if (
                result
                is Outcome.Result<int, Nimblesite.Sql.Model.SqlError>.Error<
                    int,
                    Nimblesite.Sql.Model.SqlError
                > error
            )
                return new BoolResult.Error<bool, SyncError>(
                    new SyncErrorDatabase(error.Value.Message)
                );
        }
        logger.LogInformation("Created sync triggers for {Table}", tableName);
        return new BoolResult.Ok<bool, SyncError>(true);
    }

    private static string Generate(TableDefinition table, string operation)
    {
        var key = table.PrimaryKey?.Columns.Single() ?? string.Empty;
        var oldKeys =
            operation == "update"
                ? LogRows(
                    table: table,
                    key: key,
                    operation: "delete",
                    rows: "deleted",
                    predicate: $"WHERE NOT EXISTS (SELECT 1 FROM inserted i WHERE i.{Quote(key)} = src.{Quote(key)})"
                )
                : "";
        return $"""
            CREATE OR ALTER TRIGGER {Quote(table.Schema)}.{Quote($"_sync_{table.Name}_{operation}")}
            ON {Quote(table.Schema)}.{Quote(table.Name)} AFTER {operation.ToUpperInvariant()} AS
            BEGIN
                SET NOCOUNT ON;
                IF TRY_CONVERT(int, SESSION_CONTEXT(N'sync_active')) = 1 RETURN;
                {oldKeys}
                {LogRows(
                table: table,
                key: key,
                operation: operation,
                rows: operation == "delete" ? "deleted" : "inserted",
                predicate: ""
            )}
            END
            """;
    }

    private static string LogRows(
        TableDefinition table,
        string key,
        string operation,
        string rows,
        string predicate
    )
    {
        var columns = string.Join(
            ", ",
            table.Columns.Select(column => $"src.{Quote(column.Name)}")
        );
        var payload =
            operation == "delete"
                ? "NULL"
                : $"(SELECT {columns} FOR JSON PATH, INCLUDE_NULL_VALUES, WITHOUT_ARRAY_WRAPPER)";
        return $"""
            INSERT INTO dbo._sync_log (table_name, pk_value, operation, payload, origin, timestamp)
            SELECT N'{Literal(
                table.Schema == "dbo" ? table.Name : $"{table.Schema}.{table.Name}"
            )}',
                (SELECT src.{Quote(
                key
            )} FOR JSON PATH, WITHOUT_ARRAY_WRAPPER), N'{operation}', {payload},
                COALESCE((SELECT value FROM dbo._sync_state WHERE [key] = 'origin_id'), ''),
                CONVERT(varchar(33), SYSUTCDATETIME(), 126) + 'Z'
            FROM {rows} src {predicate};
            """;
    }

    private static string Quote(string identifier) =>
        $"[{identifier.Replace("]", "]]", StringComparison.Ordinal)}]";

    private static string Literal(string value) =>
        value.Replace("'", "''", StringComparison.Ordinal);
}
