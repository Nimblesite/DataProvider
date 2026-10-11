// Implements [CI-DESLOP-INDEPENDENT-DATA].
namespace Nimblesite.Sync.Tests;

public partial class SyncIntegrationTests
{
    private static string SqlData_CreateSyncDatabase_1(global::System.String originId) =>
        $"""
            PRAGMA foreign_keys = ON;

            CREATE TABLE _sync_state (
                key TEXT PRIMARY KEY,
                value TEXT NOT NULL
            );

            CREATE TABLE _sync_session (
                sync_active INTEGER DEFAULT 0
            );
            INSERT INTO _sync_session VALUES (0);

            CREATE TABLE _sync_log (
                version INTEGER PRIMARY KEY AUTOINCREMENT,
                table_name TEXT NOT NULL,
                pk_value TEXT NOT NULL,
                operation TEXT NOT NULL CHECK (operation IN ('insert', 'update', 'delete')),
                payload TEXT,
                origin TEXT NOT NULL,
                timestamp TEXT NOT NULL
            );

            CREATE INDEX idx_sync_log_version ON _sync_log(version);

            INSERT INTO _sync_state VALUES ('origin_id', '{originId}');
            INSERT INTO _sync_state VALUES ('last_server_version', '0');

            CREATE TABLE Person (
                Id TEXT PRIMARY KEY,
                Name TEXT NOT NULL
            );

            CREATE TABLE Parent (
                Id TEXT PRIMARY KEY,
                Name TEXT NOT NULL
            );

            CREATE TABLE Child (
                Id TEXT PRIMARY KEY,
                ParentId TEXT NOT NULL REFERENCES Parent(Id),
                Name TEXT NOT NULL
            );

            -- Sync triggers for Person
            CREATE TRIGGER Person_sync_insert AFTER INSERT ON Person
            WHEN (SELECT sync_active FROM _sync_session) = 0
            BEGIN
                INSERT INTO _sync_log (table_name, pk_value, operation, payload, origin, timestamp)
                VALUES ('Person', json_object('Id', NEW.Id), 'insert',
                        json_object('Id', NEW.Id, 'Name', NEW.Name),
                        (SELECT value FROM _sync_state WHERE key = 'origin_id'),
                        strftime('%Y-%m-%dT%H:%M:%fZ', 'now'));
            END;

            CREATE TRIGGER Person_sync_update AFTER UPDATE ON Person
            WHEN (SELECT sync_active FROM _sync_session) = 0
            BEGIN
                INSERT INTO _sync_log (table_name, pk_value, operation, payload, origin, timestamp)
                VALUES ('Person', json_object('Id', NEW.Id), 'update',
                        json_object('Id', NEW.Id, 'Name', NEW.Name),
                        (SELECT value FROM _sync_state WHERE key = 'origin_id'),
                        strftime('%Y-%m-%dT%H:%M:%fZ', 'now'));
            END;

            CREATE TRIGGER Person_sync_delete AFTER DELETE ON Person
            WHEN (SELECT sync_active FROM _sync_session) = 0
            BEGIN
                INSERT INTO _sync_log (table_name, pk_value, operation, payload, origin, timestamp)
                VALUES ('Person', json_object('Id', OLD.Id), 'delete', NULL,
                        (SELECT value FROM _sync_state WHERE key = 'origin_id'),
                        strftime('%Y-%m-%dT%H:%M:%fZ', 'now'));
            END;

            -- Sync triggers for Parent
            CREATE TRIGGER Parent_sync_insert AFTER INSERT ON Parent
            WHEN (SELECT sync_active FROM _sync_session) = 0
            BEGIN
                INSERT INTO _sync_log (table_name, pk_value, operation, payload, origin, timestamp)
                VALUES ('Parent', json_object('Id', NEW.Id), 'insert',
                        json_object('Id', NEW.Id, 'Name', NEW.Name),
                        (SELECT value FROM _sync_state WHERE key = 'origin_id'),
                        strftime('%Y-%m-%dT%H:%M:%fZ', 'now'));
            END;

            -- Sync triggers for Child
            CREATE TRIGGER Child_sync_insert AFTER INSERT ON Child
            WHEN (SELECT sync_active FROM _sync_session) = 0
            BEGIN
                INSERT INTO _sync_log (table_name, pk_value, operation, payload, origin, timestamp)
                VALUES ('Child', json_object('Id', NEW.Id), 'insert',
                        json_object('Id', NEW.Id, 'ParentId', NEW.ParentId, 'Name', NEW.Name),
                        (SELECT value FROM _sync_state WHERE key = 'origin_id'),
                        strftime('%Y-%m-%dT%H:%M:%fZ', 'now'));
            END;
            """;
}
