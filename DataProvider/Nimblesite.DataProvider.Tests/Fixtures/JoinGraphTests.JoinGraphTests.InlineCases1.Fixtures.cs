// Implements [CI-TEST-SHARED-CASE-MATRIX].

namespace Nimblesite.DataProvider.Tests;

public partial class JoinGraphTests
{
    /// <summary>Returns the scenario inputs.</summary>
    public static IEnumerable<object?[]> Add_EmptyParameters_StillAddsInlineCases()
    {
        yield return ["", "Posts", "condition"];
        yield return ["Users", "", "condition"];
        yield return ["Users", "Posts", ""];
    }
}
