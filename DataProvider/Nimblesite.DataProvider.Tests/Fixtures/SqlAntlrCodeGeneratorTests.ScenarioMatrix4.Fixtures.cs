// Implements [CI-TEST-SHARED-CASE-MATRIX].
namespace Nimblesite.DataProvider.Tests;

public partial class SqlAntlrCodeGeneratorTests
{
    /// <summary>Independent input and expected-value rows for the named scenarios.</summary>
    public static System.Collections.Generic.IEnumerable<object?[]> IndependentScenarioData4()
    {
        {
            yield return ["Rejects_empty_file_name", "", "SELECT 1"];
        }
        {
            yield return ["Rejects_empty_sql", "Order", ""];
        }
    }
}
