// Implements [CI-DESLOP-INDEPENDENT-DATA].
namespace Nimblesite.Sync.SQLite.Tests;

public partial class SqliteExtensionIntegrationTests
{
    private const string Data_GetOldestSyncLogVersion_WithEntries_ReturnsMinVersion_1 = """
        INSERT INTO _sync_log (table_name, pk_value, operation, payload, origin, timestamp)
        VALUES ('Orders', '{"Id":"o1"}', 'insert', '{}', @origin, @ts);
        INSERT INTO _sync_log (table_name, pk_value, operation, payload, origin, timestamp)
        VALUES ('Orders', '{"Id":"o2"}', 'insert', '{}', @origin, @ts);
        """;

    private const string Data_PurgeTombstones_RemovesDeletesBeforeVersion_1 = """
        INSERT INTO _sync_log (table_name, pk_value, operation, payload, origin, timestamp)
        VALUES ('Orders', '{"Id":"o1"}', 'delete', NULL, @origin, @ts);
        INSERT INTO _sync_log (table_name, pk_value, operation, payload, origin, timestamp)
        VALUES ('Orders', '{"Id":"o2"}', 'insert', '{}', @origin, @ts);
        """;

    private const string Data_PurgeSyncLog_RemovesAllBeforeVersion_1 = """
        INSERT INTO _sync_log (table_name, pk_value, operation, payload, origin, timestamp)
        VALUES ('Orders', '{"Id":"o1"}', 'insert', '{}', @origin, @ts);
        INSERT INTO _sync_log (table_name, pk_value, operation, payload, origin, timestamp)
        VALUES ('Orders', '{"Id":"o2"}', 'update', '{}', @origin, @ts);
        """;
}
