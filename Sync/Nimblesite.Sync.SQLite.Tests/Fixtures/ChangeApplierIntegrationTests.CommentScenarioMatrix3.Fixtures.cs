// Implements [CI-TEST-SHARED-CASE-MATRIX].
namespace Nimblesite.Sync.SQLite.Tests;

public partial class ChangeApplierIntegrationTests
{
    /// <summary>Independent named inputs for the shared scenario runner.</summary>
    public static System.Collections.Generic.IEnumerable<object?[]> IndependentCommentScenarioData3()
    {
        {
            global::System.Func<global::Nimblesite.Sync.Core.SyncLogEntry> scenario1 =
                Fixture_ApplyChange_Insert_HandlesBooleanValues_1;
            yield return ["ApplyChange_Insert_HandlesBooleanValues", scenario1, 1L];
        }
        {
            global::System.Func<global::Nimblesite.Sync.Core.SyncLogEntry> scenario1 =
                Fixture_ApplyChange_Insert_HandlesFalseBooleanValues_1;
            yield return ["ApplyChange_Insert_HandlesFalseBooleanValues", scenario1, 0L];
        }
    }
}
