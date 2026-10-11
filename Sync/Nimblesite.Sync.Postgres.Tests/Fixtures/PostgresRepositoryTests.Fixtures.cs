// Implements [CI-DESLOP-INDEPENDENT-DATA].
namespace Nimblesite.Sync.Postgres.Tests;

public partial class PostgresRepositoryTests
{
    private static global::Nimblesite.Sync.Core.SyncLogEntry Fixture_Repositories_WithoutSyncSchema_ReturnDatabaseErrors_1() =>
        new SyncLogEntry(
            0,
            "TestTable",
            "{\"Id\":\"t1\"}",
            SyncOperation.Insert,
            "{\"Id\":\"t1\"}",
            "origin-1",
            "2025-01-01T00:00:00Z"
        );
}
