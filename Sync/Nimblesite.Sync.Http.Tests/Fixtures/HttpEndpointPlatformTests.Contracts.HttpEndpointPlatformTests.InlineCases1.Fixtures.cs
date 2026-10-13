// Implements [CI-TEST-SHARED-CASE-MATRIX].

namespace Nimblesite.Sync.Http.Tests;

public partial record HttpEndpointPlatformTests
{
    /// <summary>Returns the scenario inputs.</summary>
    public static IEnumerable<object?[]> HttpPush_ReplayUpdateDeleteAndEchoHaveIdenticalBehaviorInlineCases()
    {
        yield return ["sqlite"];
        yield return ["postgres"];
        yield return ["sqlserver"];
    }

    /// <summary>Returns the scenario inputs.</summary>
    public static IEnumerable<object?[]> LocalWrites_LogInsertUpdateDeleteAndPullInVersionOrderInlineCases()
    {
        yield return ["sqlite"];
        yield return ["postgres"];
        yield return ["sqlserver"];
    }

    /// <summary>Returns the scenario inputs.</summary>
    public static IEnumerable<object?[]> HttpPush_ForeignKeyFailureCanRetryAfterParentArrivesInlineCases()
    {
        yield return ["sqlite"];
        yield return ["postgres"];
        yield return ["sqlserver"];
    }

    /// <summary>Returns the scenario inputs.</summary>
    public static IEnumerable<object?[]> CheckConstraintFailuresReturnErrorAndLeaveConnectionUsableInlineCases()
    {
        yield return ["sqlite", false];
        yield return ["sqlite", true];
        yield return ["postgres", false];
        yield return ["postgres", true];
        yield return ["sqlserver", false];
        yield return ["sqlserver", true];
    }
}
