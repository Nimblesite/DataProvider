using System.Collections.Immutable;
using System.Text;
using Microsoft.Extensions.Logging;
using StringSyncError = Outcome.Result<string, Nimblesite.Sync.Core.SyncError>.Error<
    string,
    Nimblesite.Sync.Core.SyncError
>;
using StringSyncOk = Outcome.Result<string, Nimblesite.Sync.Core.SyncError>.Ok<
    string,
    Nimblesite.Sync.Core.SyncError
>;
using StringSyncResult = Outcome.Result<string, Nimblesite.Sync.Core.SyncError>;

namespace Nimblesite.Sync.Core;

// Implements [SYNC-TRIGGER-SHARED-GENERATION].
internal static class TriggerGenerationWorkflow
{
    internal static string Compose(string insert, string update, string delete)
    {
        var builder = new StringBuilder();
        builder.AppendLine(value: insert);
        builder.AppendLine();
        builder.AppendLine(value: update);
        builder.AppendLine();
        builder.AppendLine(value: delete);
        return builder.ToString();
    }

    internal static StringSyncResult ColumnError(string tableName, SyncError error, ILogger logger)
    {
        logger.LogError("TRIGGER: Failed to get columns for {Table}: {Error}", tableName, error);
        return new StringSyncError(error);
    }

    internal static StringSyncResult Generate<TColumn>(
        string tableName,
        IReadOnlyList<TColumn> columns,
        Func<TColumn, bool> isPrimaryKey,
        Func<TColumn, string> columnName,
        Func<string, IReadOnlyList<string>, string, string> generate,
        ILogger logger
    )
        where TColumn : class =>
        columns.Count == 0 ? MissingTable(tableName: tableName, logger: logger)
        : columns.FirstOrDefault(predicate: column => isPrimaryKey(arg: column)) is { } primaryKey
            ? Build(
                tableName: tableName,
                columns: columns,
                primaryKey: primaryKey,
                columnName: columnName,
                generate: generate,
                logger: logger
            )
        : MissingPrimaryKey(tableName: tableName, logger: logger);

    private static StringSyncResult Build<TColumn>(
        string tableName,
        IReadOnlyList<TColumn> columns,
        TColumn primaryKey,
        Func<TColumn, string> columnName,
        Func<string, IReadOnlyList<string>, string, string> generate,
        ILogger logger
    )
    {
        var primaryKeyName = columnName(arg: primaryKey);
        logger.LogDebug(
            "TRIGGER: Table {Table} has {ColumnCount} columns, PK={PK}",
            tableName,
            columns.Count,
            primaryKeyName
        );
        var triggers = generate(
            arg1: tableName,
            arg2: columns.Select(selector: column => columnName(arg: column)).ToImmutableArray(),
            arg3: primaryKeyName
        );
        logger.LogInformation(
            "TRIGGER: Generated INSERT/UPDATE/DELETE triggers for table {Table}",
            tableName
        );
        return new StringSyncOk(triggers);
    }

    private static StringSyncResult MissingTable(string tableName, ILogger logger)
    {
        logger.LogError("TRIGGER: Table {Table} not found or has no columns", tableName);
        return new StringSyncError(
            new SyncErrorDatabase($"Table '{tableName}' not found or has no columns")
        );
    }

    private static StringSyncResult MissingPrimaryKey(string tableName, ILogger logger)
    {
        logger.LogError("TRIGGER: Table {Table} has no primary key", tableName);
        return new StringSyncError(
            new SyncErrorDatabase($"Table '{tableName}' has no primary key")
        );
    }
}
