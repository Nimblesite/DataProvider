// Implements [CI-TEST-SHARED-CASE-MATRIX].
namespace Nimblesite.Sync.Tests;

public partial class SubscriptionManagerTests
{
    /// <summary>Independent input and expected-value rows for the named scenarios.</summary>
    public static System.Collections.Generic.IEnumerable<object?[]> IndependentScenarioData15()
    {
        {
            yield return ["IsExpired_FutureExpiry_ReturnsFalse", "2099-01-01T00:00:00Z"];
        }
        {
            yield return ["IsExpired_ExactExpiry_ReturnsFalse", "2024-06-01T00:00:00Z"];
        }
    }
}
