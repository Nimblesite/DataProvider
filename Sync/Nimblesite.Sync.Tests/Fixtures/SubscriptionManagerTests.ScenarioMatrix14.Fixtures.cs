// Implements [CI-TEST-SHARED-CASE-MATRIX].
namespace Nimblesite.Sync.Tests;

public partial class SubscriptionManagerTests
{
    /// <summary>Independent input and expected-value rows for the named scenarios.</summary>
    public static System.Collections.Generic.IEnumerable<object?[]> IndependentScenarioData14()
    {
        {
            yield return
            [
                "MatchesChange_RecordSubscription_MatchesPkInFilter",
                "[\"p1\", \"p2\"]",
                "{\"Id\":\"p1\"}",
            ];
        }
        {
            yield return
            [
                "MatchesChange_RecordSubscription_InvalidJsonPk_FallsBackToContains",
                "simple-filter",
                "simple-filter",
            ];
        }
    }
}
