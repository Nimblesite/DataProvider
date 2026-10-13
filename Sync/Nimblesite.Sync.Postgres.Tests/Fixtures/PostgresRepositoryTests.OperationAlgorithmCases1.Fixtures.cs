// Implements [CI-TEST-SHARED-CASE-MATRIX].

namespace Nimblesite.Sync.Postgres.Tests;

public partial class PostgresRepositoryTests
{
    /// <summary>Independent named scenario inputs and expectations.</summary>
    public static IEnumerable<object?[]> IndependentGetMaxVersion_EmptyLog_ReturnsZero_ContextCasesData()
    {
        {
            yield return
            [
                "GetMaxVersion_EmptyLog_ReturnsZero",
                new global::System.Func<
                    global::Npgsql.NpgsqlConnection,
                    global::Outcome.Result<
                        global::System.Int64,
                        global::Nimblesite.Sync.Core.SyncError
                    >
                >(PostgresSyncLogRepository.GetMaxVersion),
            ];
        }
        {
            yield return
            [
                "GetLastServerVersion_NoState_ReturnsZero",
                new global::System.Func<
                    global::Npgsql.NpgsqlConnection,
                    global::Outcome.Result<
                        global::System.Int64,
                        global::Nimblesite.Sync.Core.SyncError
                    >
                >(PostgresSyncLogRepository.GetLastServerVersion),
            ];
        }
        {
            yield return
            [
                "GetMinVersion_NoClients_ReturnsZero",
                new global::System.Func<
                    global::Npgsql.NpgsqlConnection,
                    global::Outcome.Result<
                        global::System.Int64,
                        global::Nimblesite.Sync.Core.SyncError
                    >
                >(PostgresSyncClientRepository.GetMinVersion),
            ];
        }
    }
}
