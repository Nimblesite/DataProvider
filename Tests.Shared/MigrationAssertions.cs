namespace Tests.Shared;

// Implements [MIG-TEST-SHARED-ASSERTIONS]: migration SQL translation tests share
// one "starts with a dialect prefix, then contains every fragment" check.
internal static class MigrationAssertions
{
    // Implements [MIG-TEST-SHARED-ASSERTIONS].
    internal static void AssertSqlContains(string sql, string prefix, params string[] fragments)
    {
        Xunit.Assert.StartsWith(prefix, sql, System.StringComparison.Ordinal);
        StringAssertions.ContainsAll(
            actual: sql,
            expected: [.. fragments],
            comparison: System.StringComparison.Ordinal
        );
    }
}
