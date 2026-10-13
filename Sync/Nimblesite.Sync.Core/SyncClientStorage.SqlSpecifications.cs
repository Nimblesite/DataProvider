// Implements [CI-INDEPENDENT-SQL-SPECIFICATIONS].
// Immutable SQL text only. Execution, binding and projection remain in the analysed source.
namespace Nimblesite.Sync.Core;

internal static partial class SyncClientStorage
{
    internal const string SqlSpecification1 = """
        SELECT origin_id, last_sync_version, last_sync_timestamp, created_at
        FROM _sync_clients
        ORDER BY last_sync_version ASC
        """;
    internal const string SqlSpecification2 = """
        SELECT origin_id, last_sync_version, last_sync_timestamp, created_at
        FROM _sync_clients
        WHERE origin_id = @originId
        """;
    internal const string SqlSpecification3 = """
        INSERT INTO _sync_clients (origin_id, last_sync_version, last_sync_timestamp, created_at)
        VALUES (@originId, @lastVersion, @lastTimestamp, @createdAt)
        ON CONFLICT(origin_id) DO UPDATE SET
            last_sync_version = @lastVersion,
            last_sync_timestamp = @lastTimestamp
        """;
}
