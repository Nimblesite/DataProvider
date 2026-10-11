// Implements [CI-TEST-SHARED-CASE-MATRIX].
namespace Nimblesite.Sync.SQLite.Tests;

public partial class ChangeApplierIntegrationTests
{
    /// <summary>Independent input and expected-value rows for the named scenarios.</summary>
    public static System.Collections.Generic.IEnumerable<object?[]> IndependentScenarioData12()
    {
        {
            global::System.Func<global::Nimblesite.Sync.Core.SyncLogEntry> scenario0 =
                Fixture_ApplyChange_Delete_NonExistingRecord_StillSucceeds_1;
            yield return ["ApplyChange_Delete_NonExistingRecord_StillSucceeds", scenario0];
        }
        {
            global::System.Func<global::Nimblesite.Sync.Core.SyncLogEntry> scenario0 =
                Fixture_ApplyChange_Insert_HandlesStringValues_1;
            yield return ["ApplyChange_Insert_HandlesStringValues", scenario0];
        }
    }
}
