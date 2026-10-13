// Implements [CI-TEST-SHARED-CASE-MATRIX].

namespace Nimblesite.Sync.Postgres.Tests;

public partial class PostgresRepositoryTests
{
    /// <summary>Independent named scenario inputs and expectations.</summary>
    public static IEnumerable<object?[]> EnableSuppression_Succeeds_ContextCasesData()
    {
        {
            yield return
            [
                "EnableSuppression_Succeeds",
                new global::System.Func<
                    global::Npgsql.NpgsqlConnection,
                    global::Outcome.Result<
                        global::System.Boolean,
                        global::Nimblesite.Sync.Core.SyncError
                    >
                >(PostgresSyncSession.EnableSuppression),
            ];
        }
        {
            yield return
            [
                "DisableSuppression_Succeeds",
                new global::System.Func<
                    global::Npgsql.NpgsqlConnection,
                    global::Outcome.Result<
                        global::System.Boolean,
                        global::Nimblesite.Sync.Core.SyncError
                    >
                >(PostgresSyncSession.DisableSuppression),
            ];
        }
        {
            yield return
            [
                "CreateSchema_Idempotent",
                new global::System.Func<
                    global::Npgsql.NpgsqlConnection,
                    global::Outcome.Result<
                        global::System.Boolean,
                        global::Nimblesite.Sync.Core.SyncError
                    >
                >(PostgresSyncSchema.CreateSchema),
            ];
        }
    }
}
