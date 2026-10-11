// Implements [CI-TEST-SHARED-CASE-MATRIX].

namespace Nimblesite.Lql.Tests;

public partial class LqlFileBasedTests
{
    /// <summary>Returns the scenario inputs.</summary>
    public static IEnumerable<object?[]> Joins_FileBasedTest_ShouldTransformCorrectlyInlineCases()
    {
        yield return ["join_simple", "PostgreSql"];
        yield return ["join_simple", "SqlServer"];
        yield return ["join_simple", "SQLite"];
        yield return ["join_multiple", "PostgreSql"];
        yield return ["join_multiple", "SqlServer"];
        yield return ["join_multiple", "SQLite"];
        yield return ["join_left", "PostgreSql"];
        yield return ["join_left", "SqlServer"];
        yield return ["join_left", "SQLite"];
        yield return ["complex_join_union", "PostgreSql"];
        yield return ["complex_join_union", "SqlServer"];
        yield return ["complex_join_union", "SQLite"];
    }
}
