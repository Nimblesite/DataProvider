// Implements [CI-TEST-SHARED-CASE-MATRIX].

namespace Nimblesite.Lql.Tests;

public partial class LqlFileBasedTests
{
    /// <summary>Returns the scenario inputs.</summary>
    public static IEnumerable<object?[]> Aggregation_FileBasedTest_ShouldTransformCorrectlyInlineCases()
    {
        yield return ["aggregation_groupby", "PostgreSql"];
        yield return ["aggregation_groupby", "SqlServer"];
        yield return ["aggregation_groupby", "SQLite"];
        yield return ["having_clause", "PostgreSql"];
        yield return ["having_clause", "SqlServer"];
        yield return ["having_clause", "SQLite"];
        yield return ["order_limit", "PostgreSql"];
        yield return ["order_limit", "SqlServer"];
        yield return ["order_limit", "SQLite"];
        yield return ["offset_with_limit", "PostgreSql"];
        yield return ["offset_with_limit", "SqlServer"];
        yield return ["offset_with_limit", "SQLite"];
    }
}
