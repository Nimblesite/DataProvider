// Implements [CI-INDEPENDENT-SQL-SPECIFICATIONS].
// Immutable SQL text only. Execution, binding and projection remain in the analysed source.
namespace Nimblesite.Sync.Postgres;

public static partial class PostgresSyncLogRepository
{
    internal const string SqlSpecification1 = """
        SELECT version, table_name, pk_value, operation, payload, origin, timestamp
        FROM _sync_log
        WHERE version > @fromVersion
        ORDER BY version ASC
        LIMIT @limit
        """;
    internal const string SqlSpecification2 = """
        INSERT INTO _sync_log (table_name, pk_value, operation, payload, origin, timestamp)
        VALUES (@tableName, @pkValue, @operation, @payload, @origin, @timestamp)
        """;
    internal const string SqlSpecification3 = """
        INSERT INTO _sync_state (key, value) VALUES ('last_server_version', @version)
        ON CONFLICT (key) DO UPDATE SET value = @version
        """;
    internal const string SqlSpecification4 = """
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
    internal const string SqlSpecification5 = """
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
        """;
    internal const string SqlSpecification6 = """
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
        """;
    internal const string SqlSpecification7 = """
        INSERT INTO _sync_state (key, value) VALUES ('origin_id', @originId)
        ON CONFLICT (key) DO UPDATE SET value = @originId
        """;
}
