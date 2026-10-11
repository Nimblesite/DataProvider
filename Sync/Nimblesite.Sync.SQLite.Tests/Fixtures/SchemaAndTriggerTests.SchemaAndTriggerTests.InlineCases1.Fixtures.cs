// Implements [CI-TEST-SHARED-CASE-MATRIX].
// Implements [CI-TEST-SHARED-COMMAND].

namespace Nimblesite.Sync.SQLite.Tests;

public partial class SchemaAndTriggerTests
{
    /// <summary>Returns the scenario inputs.</summary>
    public static IEnumerable<object?[]> CreateSchema_CreatesSyncLogIndexes_CasesInlineCases()
    {
        yield return
        [
            """
                SELECT name FROM sqlite_master
                WHERE type='index'
                AND name LIKE 'idx_sync_log%'
                """,
            "idx_sync_log_version",
            "idx_sync_log_table",
        ];
        yield return
        [
            """
                SELECT name FROM sqlite_master
                WHERE type='index'
                AND name LIKE 'idx_subscriptions%'
                ORDER BY name
                """,
            "idx_subscriptions_origin",
            "idx_subscriptions_table",
        ];
    }
}
