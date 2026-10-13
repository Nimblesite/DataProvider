// Implements [CI-DESLOP-INDEPENDENT-DATA].
namespace Nimblesite.Sync.SQLite.Tests;

public partial class SpecComplianceTests
{
    private const string Data_CreateTestTable_1 = """
        CREATE TABLE Person (
            Id TEXT PRIMARY KEY,
            Name TEXT NOT NULL,
            Email TEXT NOT NULL
        );

        CREATE TRIGGER Person_sync_insert
        AFTER INSERT ON Person
        WHEN (SELECT sync_active FROM _sync_session) = 0
        BEGIN
            INSERT INTO _sync_log (table_name, pk_value, operation, payload, origin, timestamp)
            VALUES (
                'Person',
                json_object('Id', NEW.Id),
                'insert',
                json_object('Id', NEW.Id, 'Name', NEW.Name, 'Email', NEW.Email),
                (SELECT value FROM _sync_state WHERE key = 'origin_id'),
                strftime('%Y-%m-%dT%H:%M:%fZ', 'now')
            );
        END;

        CREATE TRIGGER Person_sync_update
        AFTER UPDATE ON Person
        WHEN (SELECT sync_active FROM _sync_session) = 0
        BEGIN
            INSERT INTO _sync_log (table_name, pk_value, operation, payload, origin, timestamp)
            VALUES (
                'Person',
                json_object('Id', NEW.Id),
                'update',
                json_object('Id', NEW.Id, 'Name', NEW.Name, 'Email', NEW.Email),
                (SELECT value FROM _sync_state WHERE key = 'origin_id'),
                strftime('%Y-%m-%dT%H:%M:%fZ', 'now')
            );
        END;

        CREATE TRIGGER Person_sync_delete
        AFTER DELETE ON Person
        WHEN (SELECT sync_active FROM _sync_session) = 0
        BEGIN
            INSERT INTO _sync_log (table_name, pk_value, operation, payload, origin, timestamp)
            VALUES (
                'Person',
                json_object('Id', OLD.Id),
                'delete',
                NULL,
                (SELECT value FROM _sync_state WHERE key = 'origin_id'),
                strftime('%Y-%m-%dT%H:%M:%fZ', 'now')
            );
        END;
        """;
}
