// Implements [CI-TEST-SHARED-CASE-MATRIX].
// Implements [CI-TEST-SHARED-COMMAND-PARAMETERS].
// Implements [CI-TEST-SHARED-COMMAND].

namespace Nimblesite.Sync.Http.Tests;

public partial class HttpEndpointTests
{
    /// <summary>Returns the scenario inputs.</summary>
    public static IEnumerable<object?[]> PullChanges_WithoutConnectionString_ReturnsBadRequestInlineCases()
    {
        yield return ["sqlite"];
        yield return ["postgres"];
        yield return ["sqlserver"];
    }

    /// <summary>Returns the scenario inputs.</summary>
    public static IEnumerable<object?[]> PushChanges_WithoutBody_ReturnsBadRequestInlineCases()
    {
        yield return ["sqlite"];
        yield return ["postgres"];
        yield return ["sqlserver"];
    }

    /// <summary>Returns the scenario inputs.</summary>
    public static IEnumerable<object?[]> RegisterClient_WithoutOriginId_ReturnsBadRequestInlineCases()
    {
        yield return ["sqlite"];
        yield return ["postgres"];
        yield return ["sqlserver"];
    }

    /// <summary>Returns the scenario inputs.</summary>
    public static IEnumerable<object?[]> RegisterClient_WithoutConnectionString_ReturnsBadRequestInlineCases()
    {
        yield return ["sqlite"];
        yield return ["postgres"];
        yield return ["sqlserver"];
    }

    /// <summary>Returns the scenario inputs.</summary>
    public static IEnumerable<object?[]> SyncState_WithoutConnectionString_ReturnsBadRequestInlineCases()
    {
        yield return ["sqlite"];
        yield return ["postgres"];
        yield return ["sqlserver"];
    }
}
