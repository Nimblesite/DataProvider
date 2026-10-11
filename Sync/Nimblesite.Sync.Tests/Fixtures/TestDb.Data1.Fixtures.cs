// Implements [CI-DESLOP-INDEPENDENT-DATA].
namespace Nimblesite.Sync.Tests;

public partial class TestDb
{
    private const string Data_InitializeSyncSchema_1 = """
        -- Sync state (persistent)
        CREATE TABLE _sync_state (
            key TEXT PRIMARY KEY,
            value TEXT NOT NULL
        );

        -- Sync session (ephemeral flag)
        CREATE TABLE _sync_session (
            sync_active INTEGER DEFAULT 0
        );
        INSERT INTO _sync_session VALUES (0);

        -- Change log
        CREATE TABLE _sync_log (
            version     INTEGER PRIMARY KEY AUTOINCREMENT,
            table_name  TEXT NOT NULL,
            pk_value    TEXT NOT NULL,
            operation   TEXT NOT NULL CHECK (operation IN ('insert', 'update', 'delete')),
            payload     TEXT,
            origin      TEXT NOT NULL,
            timestamp   TEXT NOT NULL
        );

        CREATE INDEX idx_sync_log_version ON _sync_log(version);
        CREATE INDEX idx_sync_log_table ON _sync_log(table_name, version);

        -- Initialize state
        INSERT INTO _sync_state VALUES ('origin_id', 'test-origin-001');
        INSERT INTO _sync_state VALUES ('last_server_version', '0');
        INSERT INTO _sync_state VALUES ('last_push_version', '0');

        -- Test table with FK for testing defer/retry
        CREATE TABLE Parent (
            Id TEXT PRIMARY KEY,
            Name TEXT NOT NULL
        );

        CREATE TABLE Child (
            Id TEXT PRIMARY KEY,
            ParentId TEXT NOT NULL REFERENCES Parent(Id),
            Name TEXT NOT NULL
        );
        """;

    private const string Data_FetchChanges_1 = """
        SELECT version, table_name, pk_value, operation, payload, origin, timestamp
        FROM _sync_log
        WHERE version > $fromVersion
        ORDER BY version ASC
        LIMIT $limit
        """;
}
