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
    )
    {
        return TriggerGenerationWorkflow.Compose(
            insert: GenerateChangeTrigger(
                tableName: tableName,
                columns: columns,
                pkColumn: pkColumn,
                operation: "insert",
                eventName: "INSERT"
            ),
            update: GenerateChangeTrigger(
                tableName: tableName,
                columns: columns,
                pkColumn: pkColumn,
                operation: "update",
                eventName: "UPDATE"
            ),
            delete: GenerateDeleteTrigger(tableName: tableName, pkColumn: pkColumn)
        );
    }

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
    )
    {
        logger.LogDebug("TRIGGER: Generating triggers from schema for table {Table}", tableName);

        return GetTableColumns(connection: connection, tableName: tableName) switch
        {
            ColumnInfoListError error => TriggerGenerationWorkflow.ColumnError(
                tableName: tableName,
                error: error.Value,
                logger: logger
            ),
            ColumnInfoListOk columns => TriggerGenerationWorkflow.Generate(
                tableName: tableName,
                columns: columns.Value,
                isPrimaryKey: column => column.IsPrimaryKey,
                columnName: column => column.Name,
                generate: GenerateTriggers,
                logger: logger
            ),
        };
    }

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
    )
    {
        logger.LogDebug("TRIGGER: Creating triggers for table {Table}", tableName);

        // Drop existing triggers first to allow re-creation
        var dropResult = DropTriggers(connection, tableName, logger);
        if (dropResult is BoolSyncError dropError)
        {
            return dropError;
        }

        var triggersResult = GenerateTriggersFromSchema(connection, tableName, logger);

        if (triggersResult is StringSyncError error)
        {
            return new BoolSyncError(error.Value);
        }

        var triggers = ((StringSyncOk)triggersResult).Value;

        try
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = triggers;
            cmd.ExecuteNonQuery();

            logger.LogInformation("TRIGGER: Created triggers for table {Table}", tableName);
            return new BoolSyncOk(true);
        }
        catch (SqliteException ex)
        {
            logger.LogError(ex, "TRIGGER: Failed to create triggers for {Table}", tableName);
            return new BoolSyncError(
                new SyncErrorDatabase($"Failed to create triggers: {ex.Message}")
            );
        }
    }

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
        logger.LogDebug("TRIGGER: Dropping triggers for table {Table}", tableName);

        try
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

            logger.LogInformation("TRIGGER: Dropped triggers for table {Table}", tableName);
            return new BoolSyncOk(true);
        }
        catch (SqliteException ex)
        {
            logger.LogError(ex, "TRIGGER: Failed to drop triggers for {Table}", tableName);
            return new BoolSyncError(
                new SyncErrorDatabase($"Failed to drop triggers: {ex.Message}")
            );
        }
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
        try
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = $"PRAGMA table_info({tableName})";

            var columns = new List<TriggerColumnInfo>();
            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                // PRAGMA table_info pk column: 0=not PK, 1+=position in composite PK
                // So any non-zero value means this column is part of the primary key
                columns.Add(
                    new TriggerColumnInfo(
                        Name: reader.GetString(1),
                        Type: reader.GetString(2),
                        IsPrimaryKey: reader.GetInt32(5) > 0
                    )
                );
            }

            return new ColumnInfoListOk(columns);
        }
        catch (SqliteException ex)
        {
            return new ColumnInfoListError(
                new SyncErrorDatabase($"Failed to get table columns: {ex.Message}")
            );
        }
    }

    // Implements [SYNC-TRIGGER-SHARED-EVENT-TEMPLATE].
    private static readonly CompositeFormat ChangeTriggerFormat = CompositeFormat.Parse(
        format: """
        CREATE TRIGGER {0}_sync_{3}
        AFTER {4} ON {0}
        WHEN (SELECT sync_active FROM _sync_session) = 0
        BEGIN
            INSERT INTO _sync_log (table_name, pk_value, operation, payload, origin, timestamp)
            VALUES (
                '{0}',
                json_object('{1}', NEW.{1}),
                '{3}',
                {2},
                (SELECT value FROM _sync_state WHERE key = 'origin_id'),
                strftime('%Y-%m-%dT%H:%M:%fZ', 'now')
            );
        END;
        """
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
        string.Format(
            CultureInfo.InvariantCulture,
            """
            CREATE TRIGGER {0}_sync_delete
            AFTER DELETE ON {0}
            WHEN (SELECT sync_active FROM _sync_session) = 0
            BEGIN
                INSERT INTO _sync_log (table_name, pk_value, operation, payload, origin, timestamp)
                VALUES (
                    '{0}',
                    json_object('{1}', OLD.{1}),
                    'delete',
                    NULL,
                    (SELECT value FROM _sync_state WHERE key = 'origin_id'),
                    strftime('%Y-%m-%dT%H:%M:%fZ', 'now')
                );
            END;
            """,
            tableName,
            pkColumn
        );

    private static string BuildJsonObject(IReadOnlyList<string> columns, string prefix)
    {
        var pairs = columns.Select(c => $"'{c}', {prefix}.{c}");
        return $"json_object({string.Join(", ", pairs)})";
    }
}
