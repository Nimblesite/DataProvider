// Implements [CI-TEST-SHARED-CASE-MATRIX].
namespace Nimblesite.DataProvider.Example.Tests;

public partial class CoreCoverageTests
{
    /// <summary>Independent input and expected-value rows for the named scenarios.</summary>
    public static System.Collections.Generic.IEnumerable<object?[]> IndependentScenarioData2()
    {
        {
            yield return ["Scalar_WithEmptySql_ReturnsError", ""];
        }
        {
            yield return ["Scalar_WithInvalidSql_ReturnsError", "SELECT FROM INVALID"];
        }
    }
}
