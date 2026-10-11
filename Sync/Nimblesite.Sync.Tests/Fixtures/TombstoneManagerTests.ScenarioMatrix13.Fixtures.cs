// Implements [CI-TEST-SHARED-CASE-MATRIX].
namespace Nimblesite.Sync.Tests;

public partial class TombstoneManagerTests
{
    /// <summary>Independent input and expected-value rows for the named scenarios.</summary>
    public static System.Collections.Generic.IEnumerable<object?[]> IndependentScenarioData13()
    {
        {
            yield return ["RequiresFullResync_ClientAhead_ReturnsFalse", 50];
        }
        {
            yield return ["RequiresFullResync_ClientAtOldest_ReturnsFalse", 100];
        }
    }
}
