using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;

// Implements [SYNC-TRIGGER-SHARED-GENERATION].
namespace Nimblesite.Sync.Postgres;

/// <summary>
/// Column information for trigger generation.
/// </summary>
/// <param name="Name">Column name.</param>
/// <param name="Type">Column type.</param>
/// <param name="IsPrimaryKey">Whether this is the primary key.</param>
public sealed record TriggerColumnInfo(string Name, string Type, bool IsPrimaryKey);

/// <summary>
/// Generates sync triggers for PostgreSQL tables.
/// Implements spec Section 9 (LQL Trigger Generation) for PostgreSQL dialect.
/// </summary>
public static class PostgresTriggerGenerator
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
    /// <param name="connection">PostgreSQL connection.</param>
    /// <param name="tableName">Table to generate triggers for.</param>
    /// <param name="logger">Logger for trigger generation.</param>
    /// <returns>Trigger SQL or database error.</returns>
    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "Information schema query is safe metadata query"
    )]
    public static StringSyncResult GenerateTriggersFromSchema(
        NpgsqlConnection connection,
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
    /// <param name="connection">PostgreSQL connection.</param>
    /// <param name="tableName">Table to create triggers for.</param>
    /// <param name="logger">Logger for trigger creation.</param>
    /// <returns>Success or database error.</returns>
    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "Trigger DDL is generated from trusted schema metadata"
    )]
    public static BoolSyncResult CreateTriggers(
        NpgsqlConnection connection,
        string tableName,
        ILogger logger
    ) =>
        TriggerGenerationWorkflow.Create<NpgsqlConnection, NpgsqlException>(
            connection: connection,
            tableName: tableName,
            generate: () =>
                GenerateTriggersFromSchema(
                    connection: connection,
                    tableName: tableName,
                    logger: logger
                ),
            configure: (command, sql) => command.CommandText = sql,
            logger: logger
        );

    /// <summary>
    /// Drops sync triggers for a table.
    /// </summary>
    /// <param name="connection">PostgreSQL connection.</param>
    /// <param name="tableName">Table to drop triggers for.</param>
    /// <param name="logger">Logger for trigger operations.</param>
    /// <returns>Success or database error.</returns>
    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "Table name is used for DROP TRIGGER IF EXISTS which is safe"
    )]
    public static BoolSyncResult DropTriggers(
        NpgsqlConnection connection,
        string tableName,
        ILogger logger
    )
    {
        return TriggerGenerationWorkflow.Drop<NpgsqlException>(
            tableName: tableName,
            logger: logger,
            execute: () =>
            {
                using var cmd = connection.CreateCommand();
                var lowerTable = tableName.ToLowerInvariant();
                cmd.CommandText = string.Format(
                    CultureInfo.InvariantCulture,
                    """
                    DROP TRIGGER IF EXISTS {0}_sync_insert ON {1};
                    DROP TRIGGER IF EXISTS {0}_sync_update ON {1};
                    DROP TRIGGER IF EXISTS {0}_sync_delete ON {1};
                    DROP FUNCTION IF EXISTS {0}_sync_insert_fn();
                    DROP FUNCTION IF EXISTS {0}_sync_update_fn();
                    DROP FUNCTION IF EXISTS {0}_sync_delete_fn();
                    """,
                    lowerTable,
                    tableName
                );
                cmd.ExecuteNonQuery();
            }
        );
    }

    /// <summary>
    /// Gets column information for a table.
    /// </summary>
    /// <param name="connection">PostgreSQL connection.</param>
    /// <param name="tableName">Table name.</param>
    /// <returns>List of columns or database error.</returns>
    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "Information schema query is safe metadata query"
    )]
    public static ColumnInfoListResult GetTableColumns(
        NpgsqlConnection connection,
        string tableName
    )
    {
        return SyncCommandExecution.Read<NpgsqlException, ColumnInfoListResult>(
            connection: connection,
            setSql: command =>
                command.CommandText = global::Nimblesite
                    .Sync
                    .Postgres
                    .PostgresSyncLogRepository
                    .SqlSpecification4,
            parameters: () => [("@tableName", tableName.ToLowerInvariant())],
            read: command =>
            {
                return new ColumnInfoListOk(
                    SyncCommandExecution.ReadRows<TriggerColumnInfo>(
                        command: command,
                        read: row => new TriggerColumnInfo(
                            Name: row.GetString(0),
                            Type: row.GetString(1),
                            IsPrimaryKey: row.GetBoolean(2)
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
        format: global::Nimblesite.Sync.Postgres.PostgresSyncLogRepository.SqlSpecification5
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
                tableName.ToLowerInvariant(),
                tableName,
                pkColumn,
                BuildJsonbObject(columns: columns, prefix: "NEW"),
                pkColumn.ToLowerInvariant(),
                operation,
                eventName,
            ]
        );

    private static string GenerateDeleteTrigger(string tableName, string pkColumn)
    {
        var lowerTable = tableName.ToLowerInvariant();
        var pkLower = pkColumn.ToLowerInvariant();
        return string.Format(
            CultureInfo.InvariantCulture,
            DeleteTriggerFormat,
            lowerTable,
            tableName,
            pkColumn,
            pkLower
        );
    }

    // BUG8 fix: quote column names in jsonb_build_object payload so triggers
    // work on tables with mixed-case column names (e.g. "GivenName"). Without
    // the surrounding " quotes, PG case-folds NEW.GivenName to NEW.givenname
    // and the trigger fires `record "new" has no field "givenname"`.
    //
    // The emitted JSON key is the column name lower-cased so downstream
    // consumers see a single canonical casing regardless of how the schema
    // declared the column. The PG identifier (NEW."ColName") preserves case
    // so the trigger function compiles + executes against the real column.
    private static string BuildJsonbObject(IReadOnlyList<string> columns, string prefix)
    {
        var pairs = columns.Select(c => $"'{c.ToLowerInvariant()}', {prefix}.\"{c}\"");
        return $"jsonb_build_object({string.Join(", ", pairs)})";
    }

    // Implements [SYNC-TRIGGER-SHARED-GENERATION].
    private static readonly global::System.Text.CompositeFormat DeleteTriggerFormat =
        global::System.Text.CompositeFormat.Parse(
            format: global::Nimblesite.Sync.Postgres.PostgresSyncLogRepository.SqlSpecification6
        );
}
