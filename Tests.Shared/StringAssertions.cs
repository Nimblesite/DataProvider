using System.Collections.Immutable;

namespace Tests.Shared;

// Implements [CI-TEST-SHARED-STRING-ASSERTIONS].
internal static class StringAssertions
{
    internal static void ContainsAll(string? actual, ImmutableArray<string> expected) =>
        ContainsAll(actual: actual, expected: expected, comparison: StringComparison.CurrentCulture);

    internal static void ContainsAll(
        string? actual,
        ImmutableArray<string> expected,
        StringComparison comparison
    )
    {
        foreach (var fragment in expected)
        {
            Xunit.Assert.Contains(
                expectedSubstring: fragment,
                actualString: actual,
                comparisonType: comparison
            );
        }
    }
}
