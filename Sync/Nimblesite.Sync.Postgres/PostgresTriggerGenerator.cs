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
    )
    {
        logger.LogDebug("TRIGGER: Creating triggers for table {Table}", tableName);

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
        catch (NpgsqlException ex)
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
        logger.LogDebug("TRIGGER: Dropping triggers for table {Table}", tableName);

        try
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

            logger.LogInformation("TRIGGER: Dropped triggers for table {Table}", tableName);
            return new BoolSyncOk(true);
        }
        catch (NpgsqlException ex)
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
        try
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = """
                SELECT 
                    c.column_name,
                    c.data_type,
                    CASE WHEN pk.column_name IS NOT NULL THEN true ELSE false END as is_pk
                FROM information_schema.columns c
                LEFT JOIN (
                    SELECT kcu.column_name
                    FROM information_schema.table_constraints tc
                    JOIN information_schema.key_column_usage kcu 
                        ON tc.constraint_name = kcu.constraint_name
                        AND tc.table_schema = kcu.table_schema
                    WHERE tc.table_name = @tableName
                        AND tc.constraint_type = 'PRIMARY KEY'
                ) pk ON c.column_name = pk.column_name
                WHERE c.table_name = @tableName
                    AND c.table_schema = 'public'
                ORDER BY c.ordinal_position
                """;
            cmd.Parameters.AddWithValue("@tableName", tableName.ToLowerInvariant());

            var columns = new List<TriggerColumnInfo>();
            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                columns.Add(
                    new TriggerColumnInfo(
                        Name: reader.GetString(0),
                        Type: reader.GetString(1),
                        IsPrimaryKey: reader.GetBoolean(2)
                    )
                );
            }

            return new ColumnInfoListOk(columns);
        }
        catch (NpgsqlException ex)
        {
            return new ColumnInfoListError(
                new SyncErrorDatabase($"Failed to get table columns: {ex.Message}")
            );
        }
    }

    // Implements [SYNC-TRIGGER-SHARED-EVENT-TEMPLATE].
    private static readonly CompositeFormat ChangeTriggerFormat = CompositeFormat.Parse(
        format: """
        CREATE OR REPLACE FUNCTION {0}_sync_{5}_fn() RETURNS TRIGGER AS $$
        BEGIN
            IF (SELECT sync_active FROM _sync_session LIMIT 1) = 0 THEN
                INSERT INTO _sync_log (table_name, pk_value, operation, payload, origin, timestamp)
                VALUES (
                    '{1}',
                    jsonb_build_object('{4}', NEW."{2}")::text,
                    '{5}',
                    {3}::text,
                    (SELECT value FROM _sync_state WHERE key = 'origin_id'),
                    to_char(now() AT TIME ZONE 'UTC', 'YYYY-MM-DD"T"HH24:MI:SS.MS"Z"')
                );
            END IF;
            RETURN NEW;
        END;
        $$ LANGUAGE plpgsql;

        DROP TRIGGER IF EXISTS {0}_sync_{5} ON "{1}";
        CREATE TRIGGER {0}_sync_{5}
        AFTER {6} ON "{1}"
        FOR EACH ROW EXECUTE FUNCTION {0}_sync_{5}_fn();
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
            """
            CREATE OR REPLACE FUNCTION {0}_sync_delete_fn() RETURNS TRIGGER AS $$
            BEGIN
                IF (SELECT sync_active FROM _sync_session LIMIT 1) = 0 THEN
                    INSERT INTO _sync_log (table_name, pk_value, operation, payload, origin, timestamp)
                    VALUES (
                        '{1}',
                        jsonb_build_object('{3}', OLD."{2}")::text,
                        'delete',
                        NULL,
                        (SELECT value FROM _sync_state WHERE key = 'origin_id'),
                        to_char(now() AT TIME ZONE 'UTC', 'YYYY-MM-DD"T"HH24:MI:SS.MS"Z"')
                    );
                END IF;
                RETURN OLD;
            END;
            $$ LANGUAGE plpgsql;

            DROP TRIGGER IF EXISTS {0}_sync_delete ON "{1}";
            CREATE TRIGGER {0}_sync_delete
            AFTER DELETE ON "{1}"
            FOR EACH ROW EXECUTE FUNCTION {0}_sync_delete_fn();
            """,
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
}
