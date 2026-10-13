// Implements [CI-TEST-SHARED-CASE-MATRIX].

namespace Nimblesite.Lql.Tests;

public partial class LqlFileBasedTests
{
    /// <summary>Returns the scenario inputs.</summary>
    public static IEnumerable<object?[]> Arithmetic_FileBasedTest_ShouldTransformCorrectlyInlineCases()
    {
        yield return ["arithmetic_basic", "PostgreSql"];
        yield return ["arithmetic_basic", "SqlServer"];
        yield return ["arithmetic_basic", "SQLite"];
        yield return ["arithmetic_brackets", "PostgreSql"];
        yield return ["arithmetic_brackets", "SqlServer"];
        yield return ["arithmetic_brackets", "SQLite"];
        yield return ["arithmetic_comparisons", "PostgreSql"];
        yield return ["arithmetic_comparisons", "SqlServer"];
        yield return ["arithmetic_comparisons", "SQLite"];
        yield return ["arithmetic_functions", "PostgreSql"];
        yield return ["arithmetic_functions", "SqlServer"];
        yield return ["arithmetic_functions", "SQLite"];
        yield return ["arithmetic_case", "PostgreSql"];
        yield return ["arithmetic_case", "SqlServer"];
        yield return ["arithmetic_case", "SQLite"];
        yield return ["arithmetic_complex_nested", "PostgreSql"];
        yield return ["arithmetic_complex_nested", "SqlServer"];
        yield return ["arithmetic_complex_nested", "SQLite"];
        yield return ["arithmetic_simple", "PostgreSql"];
        yield return ["arithmetic_simple", "SqlServer"];
        yield return ["arithmetic_simple", "SQLite"];
    }
}
