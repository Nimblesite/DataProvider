using System.Collections.Immutable;
using System.Data;
using System.Text.Json;
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

/// <summary>Applies validated, parameterized changes to SQL Server. Implements [SYNC-HTTP-PROVIDERS].</summary>
public static class SqlServerChangeApplier
{
    /// <summary>Applies a change atomically; returns false for a deferred foreign key violation.</summary>
    public static BoolResult ApplyChange(
        SqlConnection connection,
        SyncLogEntry entry,
        ILogger logger
    )
    {
        try
        {
            logger.LogDebug("Applying {Operation} to {Table}", entry.Operation, entry.TableName);
            var schema = SqlServerSchemaInspector.Inspect(connection: connection);
            var table = schema is SchemaOk inspected
                ? inspected.Value.Tables.SingleOrDefault(candidate =>
                    !candidate.Name.StartsWith("_sync_", StringComparison.Ordinal)
                    && (
                        candidate.Name == entry.TableName
                        || $"{candidate.Schema}.{candidate.Name}" == entry.TableName
                    )
                )
                : null;
            return table is { PrimaryKey.Columns.Count: 1 }
                ? ApplyValidated(connection: connection, entry: entry, table: table)
                : Error(message: "Sync requires an existing table with one primary key");
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Failed to apply {Operation} to {Table}",
                entry.Operation,
                entry.TableName
            );
            return Error(message: exception.Message);
        }
    }

    private static BoolResult ApplyValidated(
        SqlConnection connection,
        SyncLogEntry entry,
        TableDefinition table
    )
    {
        using var primaryKey = JsonDocument.Parse(entry.PkValue);
        var key = table.PrimaryKey?.Columns.Single() ?? string.Empty;
        if (
            primaryKey.RootElement.ValueKind != JsonValueKind.Object
            || primaryKey.RootElement.EnumerateObject().Count() != 1
            || !primaryKey.RootElement.TryGetProperty(key, out var keyValue)
        )
            return Error(message: "Invalid primary key payload");
        var qualifiedTable = $"{Quote(identifier: table.Schema)}.{Quote(identifier: table.Name)}";
        return entry.Operation == SyncOperation.Delete
            ? Execute(
                connection: connection,
                sql: $"DELETE FROM {qualifiedTable} WHERE {Quote(identifier: key)} = @pk",
                parameters: [Parameter(name: "@pk", value: Value(value: keyValue))]
            )
            : ApplyRow(
                connection: connection,
                entry: entry,
                table: table,
                key: key,
                keyValue: keyValue,
                qualifiedTable: qualifiedTable
            );
    }

    private static BoolResult ApplyRow(
        SqlConnection connection,
        SyncLogEntry entry,
        TableDefinition table,
        string key,
        JsonElement keyValue,
        string qualifiedTable
    )
    {
        if (
            entry.Operation is not (SyncOperation.Insert or SyncOperation.Update)
            || entry.Payload is null
        )
            return Error(message: "Insert/update requires a payload");
        using var payload = JsonDocument.Parse(entry.Payload);
        if (payload.RootElement.ValueKind != JsonValueKind.Object)
            return Error(message: "Invalid payload");
        var fields = payload.RootElement.EnumerateObject().ToImmutableArray();
        if (
            fields.IsEmpty
            || fields.Select(field => field.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count()
                != fields.Length
            || fields.Any(field =>
                !table.Columns.Any(column =>
                    column.Name == field.Name
                    && column.ComputedExpression is null
                    && !column.IsIdentity
                )
            )
        )
            return Error(message: "Payload contains unknown or unwritable columns");
        if (
            !payload.RootElement.TryGetProperty(key, out var payloadKey)
            || !Equals(Value(value: payloadKey), Value(value: keyValue))
        )
            return Error(message: "Payload primary key does not match change primary key");
        return Upsert(
            connection: connection,
            qualifiedTable: qualifiedTable,
            fields: fields,
            key: key,
            keyValue: keyValue
        );
    }

    private static BoolResult Upsert(
        SqlConnection connection,
        string qualifiedTable,
        ImmutableArray<JsonProperty> fields,
        string key,
        JsonElement keyValue
    )
    {
        var columns = string.Join(", ", fields.Select(field => Quote(identifier: field.Name)));
        var values = string.Join(", ", fields.Select((_, index) => $"@p{index}"));
        var setters = fields
            .Select((field, index) => (field.Name, Index: index))
            .Where(field => field.Name != key)
            .Select(field => $"{Quote(identifier: field.Name)} = @p{field.Index}");
        var assignments = string.Join(", ", setters);
        var update =
            assignments.Length == 0
                ? ""
                : $"UPDATE {qualifiedTable} WITH (UPDLOCK, SERIALIZABLE) SET {assignments} WHERE {Quote(identifier: key)} = @pk;";
        var sql =
            $"{update} IF NOT EXISTS (SELECT 1 FROM {qualifiedTable} WITH (UPDLOCK, SERIALIZABLE) WHERE {Quote(identifier: key)} = @pk) INSERT INTO {qualifiedTable} ({columns}) VALUES ({values});";
        return Execute(
            connection: connection,
            sql: sql,
            parameters: fields
                .Select(
                    (field, index) =>
                        Parameter(name: $"@p{index}", value: Value(value: field.Value))
                )
                .Append(Parameter(name: "@pk", value: Value(value: keyValue)))
                .ToImmutableArray()
        );
    }

    private static BoolResult Execute(
        SqlConnection connection,
        string sql,
        ImmutableArray<SqlParameter> parameters
    )
    {
        using var transaction = connection.BeginTransaction();
        var result = transaction.Execute(sql: sql, parameters: parameters);
        return result.Match<BoolResult>(
            _ =>
            {
                transaction.Commit();
                return new BoolResult.Ok<bool, SyncError>(true);
            },
            error =>
            {
                transaction.Rollback();
                return
                    error.Exception is SqlException { Number: 547 } exception
                    && exception.Message.Contains("FOREIGN KEY", StringComparison.OrdinalIgnoreCase)
                    ? new BoolResult.Ok<bool, SyncError>(false)
                    : Error(message: error.Message);
            }
        );
    }

    private static SqlParameter Parameter(string name, object value) =>
        new(parameterName: name, value: value);

    private static object Value(JsonElement value) =>
        value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? string.Empty,
            JsonValueKind.Number when value.TryGetInt64(out var integer) => integer,
            JsonValueKind.Number => value.GetDecimal(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => DBNull.Value,
            _ => value.GetRawText(),
        };

    private static string Quote(string identifier) =>
        $"[{identifier.Replace("]", "]]", StringComparison.Ordinal)}]";

    private static BoolResult Error(string message) =>
        new BoolResult.Error<bool, SyncError>(new SyncErrorDatabase(message));
}
