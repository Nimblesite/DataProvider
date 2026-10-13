// Implements [CI-TEST-SHARED-CASE-MATRIX].

namespace Nimblesite.Lql.Tests;

public partial class LqlFileBasedTests
{
    /// <summary>Returns the scenario inputs.</summary>
    public static IEnumerable<object?[]> AdvancedQueries_FileBasedTest_ShouldTransformCorrectlyInlineCases()
    {
        yield return ["window_function", "PostgreSql"];
        yield return ["window_function", "SqlServer"];
        yield return ["window_function", "SQLite"];
        yield return ["subquery_nested", "PostgreSql"];
        yield return ["subquery_nested", "SqlServer"];
        yield return ["subquery_nested", "SQLite"];
        yield return ["cte_with", "PostgreSql"];
        yield return ["cte_with", "SqlServer"];
        yield return ["cte_with", "SQLite"];
        yield return ["exists_subquery", "PostgreSql"];
        yield return ["exists_subquery", "SqlServer"];
        yield return ["exists_subquery", "SQLite"];
        yield return ["in_subquery", "PostgreSql"];
        yield return ["in_subquery", "SqlServer"];
        yield return ["in_subquery", "SQLite"];
    }
}
