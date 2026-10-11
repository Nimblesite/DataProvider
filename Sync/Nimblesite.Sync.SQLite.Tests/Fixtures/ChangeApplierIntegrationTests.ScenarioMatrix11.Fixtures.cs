// Implements [CI-TEST-SHARED-CASE-MATRIX].
namespace Nimblesite.Sync.SQLite.Tests;

public partial class ChangeApplierIntegrationTests
{
    /// <summary>Independent input and expected-value rows for the named scenarios.</summary>
    public static System.Collections.Generic.IEnumerable<object?[]> IndependentScenarioData11()
    {
        {
            global::System.Func<global::Nimblesite.Sync.Core.SyncLogEntry> scenario0 =
                Fixture_ApplyChange_Update_NonExistingRecord_InsertsAsUpsert_1;
            yield return
            [
                "ApplyChange_Update_NonExistingRecord_InsertsAsUpsert",
                scenario0,
                "SELECT COUNT(*) FROM Person WHERE Id = 'p1'",
                1L,
            ];
        }
        {
            global::System.Func<global::Nimblesite.Sync.Core.SyncLogEntry> scenario0 =
                Fixture_ApplyChange_Insert_HandlesIntegerValues_1;
            yield return
            [
                "ApplyChange_Insert_HandlesIntegerValues",
                scenario0,
                "SELECT Age FROM Person WHERE Id = 'p1'",
                25L,
            ];
        }
    }
}
