using System.Collections.Immutable;

namespace Tests.Shared;

// Implements [CI-TEST-SHARED-STRING-ASSERTIONS].
internal static class StringAssertions
{
    internal static string SqlFragments<TError>(
        Func<Outcome.Result<string, TError>> read,
        ImmutableArray<string> expected,
        StringComparison comparison = StringComparison.CurrentCulture
    )
    {
        var sql = ResultAssertions.Ok(read: read).Value;
        ContainsAll(actual: sql, expected: expected, comparison: comparison);
        return sql;
    }

    internal static void ContainsAll(string? actual, ImmutableArray<string> expected) =>
        ContainsAll(
            actual: actual,
            expected: expected,
            comparison: StringComparison.CurrentCulture
        );

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
