// Implements [CI-TEST-SHARED-CASE-MATRIX].

namespace Nimblesite.Sync.Http.Tests;

public partial record HttpEndpointPlatformTests
{
    /// <summary>Returns the scenario inputs.</summary>
    public static IEnumerable<object?[]> HttpStateAndPull_ReturnEmptyStateAndChangesInlineCases()
    {
        yield return ["sqlite"];
        yield return ["postgres"];
        yield return ["sqlserver"];
    }

    /// <summary>Returns the scenario inputs.</summary>
    public static IEnumerable<object?[]> HttpPush_AppliesChangeOnEveryProviderInlineCases()
    {
        yield return ["sqlite"];
        yield return ["postgres"];
        yield return ["sqlserver"];
    }
}
