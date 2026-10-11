// Implements [CI-TEST-SHARED-CASE-MATRIX].

namespace Nimblesite.Sync.Tests;

public partial class ConflictResolverTests
{
    /// <summary>Returns the scenario inputs.</summary>
    public static IEnumerable<object?[]> IsConflict_SameTableSamePkSameOrigin_ReturnsFalse_CasesInlineCases()
    {
        yield return ["Person", "pk-1", "origin-A"];
        yield return ["Order", "pk-1", "origin-B"];
        yield return ["Person", "pk-2", "origin-B"];
    }
}
