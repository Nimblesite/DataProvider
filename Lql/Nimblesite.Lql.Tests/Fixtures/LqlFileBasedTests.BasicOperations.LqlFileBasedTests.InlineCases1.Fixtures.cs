// Implements [CI-TEST-SHARED-CASE-MATRIX].

namespace Nimblesite.Lql.Tests;

public partial class LqlFileBasedTests
{
    /// <summary>Returns the scenario inputs.</summary>
    public static IEnumerable<object?[]> BasicOperations_FileBasedTest_ShouldTransformCorrectlyInlineCases()
    {
        yield return ["simple_select", "PostgreSql"];
        yield return ["simple_select", "SqlServer"];
        yield return ["simple_select", "SQLite"];
        yield return ["filter_simple_age", "PostgreSql"];
        yield return ["filter_simple_age", "SqlServer"];
        yield return ["filter_simple_age", "SQLite"];
        yield return ["simple_filter_and", "PostgreSql"];
        yield return ["simple_filter_and", "SqlServer"];
        yield return ["simple_filter_and", "SQLite"];
        yield return ["filter_multiple_conditions", "PostgreSql"];
        yield return ["filter_multiple_conditions", "SqlServer"];
        yield return ["filter_multiple_conditions", "SQLite"];
        yield return ["filter_complex_and_or", "PostgreSql"];
        yield return ["filter_complex_and_or", "SqlServer"];
        yield return ["filter_complex_and_or", "SQLite"];
        yield return ["filter_like", "PostgreSql"];
        yield return ["filter_like", "SqlServer"];
        yield return ["filter_like", "SQLite"];
        yield return ["select_with_alias", "PostgreSql"];
        yield return ["select_with_alias", "SqlServer"];
        yield return ["select_with_alias", "SQLite"];
    }
}
