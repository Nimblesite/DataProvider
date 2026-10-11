// Implements [CI-TEST-SHARED-CASE-MATRIX].

namespace Nimblesite.Lql.Tests;

public partial class LqlFileBasedTests
{
    /// <summary>Returns the scenario inputs.</summary>
    public static IEnumerable<object?[]> LargeQuery_TranspilesCompleteShapeForEveryDialectInlineCases()
    {
        yield return ["PostgreSql"];
        yield return ["SqlServer"];
        yield return ["SQLite"];
    }

    /// <summary>Returns the scenario inputs.</summary>
    public static IEnumerable<object?[]> RepeatedQueries_GenerateStableSqlForEveryDialectInlineCases()
    {
        yield return ["PostgreSql"];
        yield return ["SqlServer"];
        yield return ["SQLite"];
    }
}
