// Implements [CI-INDEPENDENT-SQL-SPECIFICATIONS].
// Immutable SQL text only. Execution, binding and projection remain in the analysed source.
namespace Nimblesite.Sync.SQLite;

public static partial class MappingStateRepository
{
    internal const string SqlSpecification1 = """
        SELECT mapping_id, last_synced_version, last_sync_timestamp, records_synced
        FROM _sync_mapping_state
        WHERE mapping_id = @mappingId
        """;
    internal const string SqlSpecification2 = """
        SELECT mapping_id, last_synced_version, last_sync_timestamp, records_synced
        FROM _sync_mapping_state
        ORDER BY mapping_id
        """;
    internal const string SqlSpecification3 = """
        INSERT INTO _sync_mapping_state (mapping_id, last_synced_version, last_sync_timestamp, records_synced)
        VALUES (@mappingId, @version, @timestamp, @records)
        ON CONFLICT(mapping_id) DO UPDATE SET
            last_synced_version = @version,
            last_sync_timestamp = @timestamp,
            records_synced = @records
        """;
    internal const string SqlSpecification4 = """
        SELECT mapping_id, source_pk, payload_hash, synced_at
        FROM _sync_record_hashes
        WHERE mapping_id = @mappingId AND source_pk = @sourcePk
        """;
    internal const string SqlSpecification5 = """
        INSERT INTO _sync_record_hashes (mapping_id, source_pk, payload_hash, synced_at)
        VALUES (@mappingId, @sourcePk, @hash, @syncedAt)
        ON CONFLICT(mapping_id, source_pk) DO UPDATE SET
            payload_hash = @hash,
            synced_at = @syncedAt
        """;
    internal const string SqlSpecification6 = """
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
        """;
    internal const string SqlSpecification7 = """
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
        """;
    internal const string SqlSpecification8 = """
        SELECT subscription_id, origin_id, subscription_type, table_name, filter, created_at, expires_at
        FROM _sync_subscriptions
        ORDER BY created_at ASC
        """;
    internal const string SqlSpecification9 = """
        SELECT subscription_id, origin_id, subscription_type, table_name, filter, created_at, expires_at
        FROM _sync_subscriptions
        WHERE subscription_id = @subscriptionId
        """;
    internal const string SqlSpecification10 = """
        SELECT subscription_id, origin_id, subscription_type, table_name, filter, created_at, expires_at
        FROM _sync_subscriptions
        WHERE table_name = @tableName
        ORDER BY created_at ASC
        """;
    internal const string SqlSpecification11 = """
        SELECT subscription_id, origin_id, subscription_type, table_name, filter, created_at, expires_at
        FROM _sync_subscriptions
        WHERE origin_id = @originId
        ORDER BY created_at ASC
        """;
    internal const string SqlSpecification12 = """
        INSERT INTO _sync_subscriptions
            (subscription_id, origin_id, subscription_type, table_name, filter, created_at, expires_at)
        VALUES
            (@subscriptionId, @originId, @subscriptionType, @tableName, @filter, @createdAt, @expiresAt)
        """;
    internal const string SqlSpecification13 = """
        DELETE FROM _sync_subscriptions
        WHERE expires_at IS NOT NULL AND expires_at < @currentTimestamp
        """;
    internal const string SqlSpecification14 = """
        CREATE TABLE IF NOT EXISTS _sync_state (
            key TEXT PRIMARY KEY,
            value TEXT NOT NULL
        );
        """;
    internal const string SqlSpecification15 = """
        CREATE TABLE IF NOT EXISTS _sync_session (
            sync_active INTEGER DEFAULT 0
        );
        """;
    internal const string SqlSpecification16 = """
        CREATE TABLE IF NOT EXISTS _sync_log (
            version     INTEGER PRIMARY KEY AUTOINCREMENT,
            table_name  TEXT NOT NULL,
            pk_value    TEXT NOT NULL,
            operation   TEXT NOT NULL CHECK (operation IN ('insert', 'update', 'delete')),
            payload     TEXT,
            origin      TEXT NOT NULL,
            timestamp   TEXT NOT NULL
        );

        CREATE INDEX IF NOT EXISTS idx_sync_log_version ON _sync_log(version);
        CREATE INDEX IF NOT EXISTS idx_sync_log_table ON _sync_log(table_name, version);
        """;
    internal const string SqlSpecification17 = """
        CREATE TABLE IF NOT EXISTS _sync_clients (
            origin_id TEXT PRIMARY KEY,
            last_sync_version INTEGER NOT NULL DEFAULT 0,
            last_sync_timestamp TEXT NOT NULL,
            created_at TEXT NOT NULL
        );

        CREATE INDEX IF NOT EXISTS idx_sync_clients_version ON _sync_clients(last_sync_version);
        """;
    internal const string SqlSpecification18 = """
        CREATE TABLE IF NOT EXISTS _sync_subscriptions (
            subscription_id TEXT PRIMARY KEY,
            origin_id TEXT NOT NULL,
            subscription_type TEXT NOT NULL CHECK (subscription_type IN ('record', 'table', 'query')),
            table_name TEXT NOT NULL,
            filter TEXT,
            created_at TEXT NOT NULL,
            expires_at TEXT
        );

        CREATE INDEX IF NOT EXISTS idx_subscriptions_table ON _sync_subscriptions(table_name);
        CREATE INDEX IF NOT EXISTS idx_subscriptions_origin ON _sync_subscriptions(origin_id);
        """;
    internal const string SqlSpecification19 = """
        CREATE TABLE IF NOT EXISTS _sync_mapping_state (
            mapping_id TEXT PRIMARY KEY,
            last_synced_version INTEGER NOT NULL DEFAULT 0,
            last_sync_timestamp TEXT NOT NULL,
            records_synced INTEGER NOT NULL DEFAULT 0
        );
        """;
    internal const string SqlSpecification20 = """
        CREATE TABLE IF NOT EXISTS _sync_record_hashes (
            mapping_id TEXT NOT NULL,
            source_pk TEXT NOT NULL,
            payload_hash TEXT NOT NULL,
            synced_at TEXT NOT NULL,
            PRIMARY KEY (mapping_id, source_pk)
        );

        CREATE INDEX IF NOT EXISTS idx_record_hashes_mapping ON _sync_record_hashes(mapping_id);
        """;
    internal const string SqlSpecification21 = """
        INSERT OR IGNORE INTO _sync_state (key, value) VALUES ('origin_id', '');
        INSERT OR IGNORE INTO _sync_state (key, value) VALUES ('last_server_version', '0');
        INSERT OR IGNORE INTO _sync_state (key, value) VALUES ('last_push_version', '0');
        INSERT OR IGNORE INTO _sync_session (sync_active) VALUES (0);
        """;
    internal const string SqlSpecification22 = """
        SELECT subscription_id, origin_id, subscription_type, table_name, filter, created_at, expires_at
        FROM _sync_subscriptions
        WHERE table_name = @tableName
        """;
    internal const string SqlSpecification23 = """
        DELETE FROM _sync_log
        WHERE operation = 'delete'
          AND version < @belowVersion
        """;
    internal const string SqlSpecification24 = """
        SELECT version, table_name, pk_value, operation, payload, origin, timestamp
        FROM _sync_log
        WHERE version > @fromVersion
        ORDER BY version ASC
        LIMIT @batchSize
        """;
    internal const string SqlSpecification25 = """
        INSERT INTO _sync_mapping_state (mapping_id, last_synced_version, last_sync_timestamp, records_synced)
        VALUES (@mappingId, @version, @timestamp, @records)
        ON CONFLICT (mapping_id) DO UPDATE SET
            last_synced_version = @version,
            last_sync_timestamp = @timestamp,
            records_synced = @records
        """;
    internal const string SqlSpecification26 = """
        INSERT INTO _sync_record_hashes (mapping_id, source_pk, payload_hash, synced_at)
        VALUES (@mappingId, @sourcePk, @hash, @syncedAt)
        ON CONFLICT (mapping_id, source_pk) DO UPDATE SET
            payload_hash = @hash,
            synced_at = @syncedAt
        """;
    internal const string SqlSpecification27 = """
        DELETE FROM _sync_record_hashes
        WHERE mapping_id = @mappingId AND source_pk = @sourcePk
        """;
}
