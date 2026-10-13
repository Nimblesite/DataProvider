using System.Collections.Immutable;
using System.Data.Common;
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

    internal static string Compose(
        string tableName,
        IReadOnlyList<string> columns,
        string pkColumn,
        Func<string, IReadOnlyList<string>, string, string, string, string> change,
        Func<string, string, string> delete
    ) =>
        Compose(
            insert: change(
                arg1: tableName,
                arg2: columns,
                arg3: pkColumn,
                arg4: "insert",
                arg5: "INSERT"
            ),
            update: change(
                arg1: tableName,
                arg2: columns,
                arg3: pkColumn,
                arg4: "update",
                arg5: "UPDATE"
            ),
            delete: delete(arg1: tableName, arg2: pkColumn)
        );

    internal static StringSyncResult FromSchema<TColumn>(
        string tableName,
        Func<Outcome.Result<IReadOnlyList<TColumn>, SyncError>> readColumns,
        Func<TColumn, bool> isPrimaryKey,
        Func<TColumn, string> columnName,
        Func<string, IReadOnlyList<string>, string, string> generate,
        ILogger logger
    )
        where TColumn : class
    {
        logger.LogDebug("TRIGGER: Generating triggers from schema for table {Table}", tableName);
        return readColumns() switch
        {
            Outcome.Result<IReadOnlyList<TColumn>, SyncError>.Error<
                IReadOnlyList<TColumn>,
                SyncError
            > error => ColumnError(tableName: tableName, error: error.Value, logger: logger),
            Outcome.Result<IReadOnlyList<TColumn>, SyncError>.Ok<
                IReadOnlyList<TColumn>,
                SyncError
            > columns => Generate(
                tableName: tableName,
                columns: columns.Value,
                isPrimaryKey: isPrimaryKey,
                columnName: columnName,
                generate: generate,
                logger: logger
            ),
        };
    }

    internal static BoolSyncResult Create<TException>(
        string tableName,
        Func<StringSyncResult> generate,
        Action<string> execute,
        ILogger logger,
        Func<BoolSyncResult>? prepare = null
    )
        where TException : Exception
    {
        logger.LogDebug("TRIGGER: Creating triggers for table {Table}", tableName);
        if (prepare?.Invoke() is BoolSyncError dropError)
        {
            return dropError;
        }
        return generate() switch
        {
            StringSyncError error => new BoolSyncError(error.Value),
            StringSyncOk triggers => ExecuteCreate<TException>(
                tableName: tableName,
                sql: triggers.Value,
                execute: execute,
                logger: logger
            ),
        };
    }

    // Implements [SYNC-TRIGGER-SHARED-GENERATION].
    internal static BoolSyncResult Create<TConnection, TException>(
        TConnection connection,
        string tableName,
        Func<StringSyncResult> generate,
        Action<DbCommand, string> configure,
        ILogger logger,
        Func<BoolSyncResult>? prepare = null
    )
        where TConnection : DbConnection
        where TException : Exception =>
        Create<TException>(
            tableName: tableName,
            generate: generate,
            execute: sql =>
            {
                using var command = connection.CreateCommand();
                configure(arg1: command, arg2: sql);
                command.ExecuteNonQuery();
            },
            logger: logger,
            prepare: prepare
        );

    private static BoolSyncResult ExecuteCreate<TException>(
        string tableName,
        string sql,
        Action<string> execute,
        ILogger logger
    )
        where TException : Exception
    {
        try
        {
            execute(obj: sql);
            logger.LogInformation("TRIGGER: Created triggers for table {Table}", tableName);
            return new BoolSyncOk(true);
        }
        catch (TException ex)
        {
            logger.LogError(ex, "TRIGGER: Failed to create triggers for {Table}", tableName);
            return new BoolSyncError(
                new SyncErrorDatabase($"Failed to create triggers: {ex.Message}")
            );
        }
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

    // Implements [SYNC-TRIGGER-SHARED-GENERATION].
    internal static BoolSyncResult Drop<TException>(
        string tableName,
        ILogger logger,
        Action execute
    )
        where TException : Exception
    {
        logger.LogDebug("TRIGGER: Dropping triggers for table {Table}", tableName);
        try
        {
            execute();
            logger.LogInformation("TRIGGER: Dropped triggers for table {Table}", tableName);
            return new BoolSyncOk(true);
        }
        catch (TException ex)
        {
            logger.LogError(ex, "TRIGGER: Failed to drop triggers for {Table}", tableName);
            return new BoolSyncError(
                new SyncErrorDatabase($"Failed to drop triggers: {ex.Message}")
            );
        }
    }
}
