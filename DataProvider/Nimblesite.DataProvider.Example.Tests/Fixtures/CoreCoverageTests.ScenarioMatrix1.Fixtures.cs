// Implements [CI-TEST-SHARED-CASE-MATRIX].
namespace Nimblesite.DataProvider.Example.Tests;

public partial class CoreCoverageTests
{
    /// <summary>Independent input and expected-value rows for the named scenarios.</summary>
    public static System.Collections.Generic.IEnumerable<object?[]> IndependentScenarioData1()
    {
        {
            yield return ["Execute_WithEmptySql_ReturnsError", "   "];
        }
        {
            yield return
            [
                "Execute_WithInvalidSql_ReturnsError",
                "INSERT INTO NonExistentTable VALUES (1)",
            ];
        }
    }
}
