using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

// Implements [SYNC-TRIGGER-SHARED-GENERATION].
namespace Nimblesite.Sync.SQLite;

/// <summary>
/// Column information for trigger generation.
/// </summary>
/// <param name="Name">Column name.</param>
/// <param name="Type">Column type.</param>
/// <param name="IsPrimaryKey">Whether this is the primary key.</param>
public sealed record TriggerColumnInfo(string Name, string Type, bool IsPrimaryKey);

/// <summary>
/// Generates sync triggers for SQLite tables.
/// Implements spec Section 9 (LQL Trigger Generation) for SQLite dialect.
/// </summary>
public static class TriggerGenerator
{
    /// <summary>
    /// Generates sync triggers for a table.
    /// Creates INSERT, UPDATE, and DELETE triggers that log changes to _sync_log.
    /// </summary>
    /// <param name="tableName">Name of the table to generate triggers for.</param>
    /// <param name="columns">List of column names to include in payload.</param>
    /// <param name="pkColumn">Primary key column name.</param>
    /// <returns>SQL DDL for all three triggers.</returns>
    public static string GenerateTriggers(
        string tableName,
        IReadOnlyList<string> columns,
        string pkColumn
    ) =>
        TriggerGenerationWorkflow.Compose(
            tableName: tableName,
            columns: columns,
            pkColumn: pkColumn,
            change: GenerateChangeTrigger,
            delete: GenerateDeleteTrigger
        );

    /// <summary>
    /// Generates triggers for a table by reading schema from database.
    /// </summary>
    /// <param name="connection">SQLite connection.</param>
    /// <param name="tableName">Table to generate triggers for.</param>
    /// <param name="logger">Logger for trigger generation.</param>
    /// <returns>Trigger SQL or database error.</returns>
    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "PRAGMA table_info is safe metadata query"
    )]
    public static StringSyncResult GenerateTriggersFromSchema(
        SqliteConnection connection,
        string tableName,
        ILogger logger
    ) =>
        TriggerGenerationWorkflow.FromSchema(
            tableName: tableName,
            readColumns: () => GetTableColumns(connection: connection, tableName: tableName),
            isPrimaryKey: column => column.IsPrimaryKey,
            columnName: column => column.Name,
            generate: GenerateTriggers,
            logger: logger
        );

    /// <summary>
    /// Creates sync triggers in the database for a table.
    /// </summary>
    /// <param name="connection">SQLite connection.</param>
    /// <param name="tableName">Table to create triggers for.</param>
    /// <param name="logger">Logger for trigger creation.</param>
    /// <returns>Success or database error.</returns>
    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "Trigger DDL is generated from trusted schema metadata"
    )]
    public static BoolSyncResult CreateTriggers(
        SqliteConnection connection,
        string tableName,
        ILogger logger
    ) =>
        TriggerGenerationWorkflow.Create<SqliteConnection, SqliteException>(
            connection: connection,
            tableName: tableName,
            generate: () =>
                GenerateTriggersFromSchema(
                    connection: connection,
                    tableName: tableName,
                    logger: logger
                ),
            configure: (command, sql) => command.CommandText = sql,
            logger: logger,
            prepare: () =>
                DropTriggers(connection: connection, tableName: tableName, logger: logger)
        );

    /// <summary>
    /// Drops sync triggers for a table.
    /// </summary>
    /// <param name="connection">SQLite connection.</param>
    /// <param name="tableName">Table to drop triggers for.</param>
    /// <param name="logger">Logger for trigger operations.</param>
    /// <returns>Success or database error.</returns>
    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "Table name is used for DROP TRIGGER IF EXISTS which is safe"
    )]
    public static BoolSyncResult DropTriggers(
        SqliteConnection connection,
        string tableName,
        ILogger logger
    )
    {
        return TriggerGenerationWorkflow.Drop<SqliteException>(
            tableName: tableName,
            logger: logger,
            execute: () =>
            {
                using var cmd = connection.CreateCommand();
                cmd.CommandText = string.Format(
                    CultureInfo.InvariantCulture,
                    """
                    DROP TRIGGER IF EXISTS {0}_sync_insert;
                    DROP TRIGGER IF EXISTS {0}_sync_update;
                    DROP TRIGGER IF EXISTS {0}_sync_delete;
                    """,
                    tableName
                );
                cmd.ExecuteNonQuery();
            }
        );
    }

    /// <summary>
    /// Gets column information for a table.
    /// </summary>
    /// <param name="connection">SQLite connection.</param>
    /// <param name="tableName">Table name.</param>
    /// <returns>List of columns or database error.</returns>
    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "PRAGMA table_info is safe metadata query"
    )]
    public static ColumnInfoListResult GetTableColumns(
        SqliteConnection connection,
        string tableName
    )
    {
        return SyncCommandExecution.Read<SqliteException, ColumnInfoListResult>(
            connection: connection,
            setSql: command => command.CommandText = $"PRAGMA table_info({tableName})",
            parameters: () => [],
            read: command =>
            {
                return new ColumnInfoListOk(
                    SyncCommandExecution.ReadRows<TriggerColumnInfo>(
                        command: command,
                        read: row => new TriggerColumnInfo(
                            Name: row.GetString(1),
                            Type: row.GetString(2),
                            IsPrimaryKey: row.GetInt32(5) > 0
                        )
                    )
                );
            },
            failure: ex =>
            {
                return new ColumnInfoListError(
                    new SyncErrorDatabase($"Failed to get table columns: {ex.Message}")
                );
            }
        );
    }

    // Implements [SYNC-TRIGGER-SHARED-EVENT-TEMPLATE].
    private static readonly CompositeFormat ChangeTriggerFormat = CompositeFormat.Parse(
        format: global::Nimblesite.Sync.SQLite.MappingStateRepository.SqlSpecification6
    );

    private static string GenerateChangeTrigger(
        string tableName,
        IReadOnlyList<string> columns,
        string pkColumn,
        string operation,
        string eventName
    ) =>
        string.Format(
            provider: CultureInfo.InvariantCulture,
            format: ChangeTriggerFormat,
            args:
            [
                tableName,
                pkColumn,
                BuildJsonObject(columns: columns, prefix: "NEW"),
                operation,
                eventName,
            ]
        );

    private static string GenerateDeleteTrigger(string tableName, string pkColumn) =>
        string.Format(CultureInfo.InvariantCulture, DeleteTriggerFormat, tableName, pkColumn);

    private static string BuildJsonObject(IReadOnlyList<string> columns, string prefix)
    {
        var pairs = columns.Select(c => $"'{c}', {prefix}.{c}");
        return $"json_object({string.Join(", ", pairs)})";
    }

    // Implements [SYNC-TRIGGER-SHARED-GENERATION].
    private static readonly global::System.Text.CompositeFormat DeleteTriggerFormat =
        global::System.Text.CompositeFormat.Parse(
            format: global::Nimblesite.Sync.SQLite.MappingStateRepository.SqlSpecification7
        );
}
