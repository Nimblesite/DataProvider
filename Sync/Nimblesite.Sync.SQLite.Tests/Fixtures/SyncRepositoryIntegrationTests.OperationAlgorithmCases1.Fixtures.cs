// Implements [CI-TEST-SHARED-CASE-MATRIX].

namespace Nimblesite.Sync.SQLite.Tests;

public partial class SyncRepositoryIntegrationTests
{
    /// <summary>Independent named scenario inputs and expectations.</summary>
    public static IEnumerable<object?[]> SyncClientRepository_GetMinVersion_EmptyDatabase_ReturnsZero_ContextCasesData()
    {
        {
            yield return
            [
                "SyncClientRepository_GetMinVersion_EmptyDatabase_ReturnsZero",
                new global::System.Func<
                    global::Microsoft.Data.Sqlite.SqliteConnection,
                    global::Outcome.Result<
                        global::System.Int64,
                        global::Nimblesite.Sync.Core.SyncError
                    >
                >(SyncClientRepository.GetMinVersion),
            ];
        }
        {
            yield return
            [
                "SyncLogRepository_GetMaxVersion_EmptyLog_ReturnsZero",
                new global::System.Func<
                    global::Microsoft.Data.Sqlite.SqliteConnection,
                    global::Outcome.Result<
                        global::System.Int64,
                        global::Nimblesite.Sync.Core.SyncError
                    >
                >(SyncLogRepository.GetMaxVersion),
            ];
        }
        {
            yield return
            [
                "SyncLogRepository_GetLastServerVersion_Default_ReturnsZero",
                new global::System.Func<
                    global::Microsoft.Data.Sqlite.SqliteConnection,
                    global::Outcome.Result<
                        global::System.Int64,
                        global::Nimblesite.Sync.Core.SyncError
                    >
                >(SyncLogRepository.GetLastServerVersion),
            ];
        }
        {
            yield return
            [
                "SyncLogRepository_GetMinVersion_EmptyLog_ReturnsZero",
                new global::System.Func<
                    global::Microsoft.Data.Sqlite.SqliteConnection,
                    global::Outcome.Result<
                        global::System.Int64,
                        global::Nimblesite.Sync.Core.SyncError
                    >
                >(SyncLogRepository.GetMinVersion),
            ];
        }
        {
            yield return
            [
                "SyncLogRepository_GetEntryCount_EmptyLog_ReturnsZero",
                new global::System.Func<
                    global::Microsoft.Data.Sqlite.SqliteConnection,
                    global::Outcome.Result<
                        global::System.Int64,
                        global::Nimblesite.Sync.Core.SyncError
                    >
                >(SyncLogRepository.GetEntryCount),
            ];
        }
    }
}
