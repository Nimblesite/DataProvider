namespace Tests.Shared;

// Implements [CI-TEST-SHARED-ROW-ASSERTIONS]: shared row-presence checks that
// replace the repeated per-field `Assert.Contains(rows, r => ...)` predicates.
// Each expected value is compared against the typed projection of every row
// using that projection's default equality, preserving the exact per-field
// equality and StringComparison semantics of the original predicates.
internal static class RowAssertions
{
    internal static void ContainsAll<TItem, TValue>(
        IEnumerable<TItem> rows,
        Func<TItem, TValue> projection,
        params TValue[] expected
    )
    {
        foreach (var value in expected)
        {
            Xunit.Assert.Contains(
                collection: rows,
                filter: item => EqualityComparer<TValue>.Default.Equals(projection(item), value)
            );
        }
    }
}
