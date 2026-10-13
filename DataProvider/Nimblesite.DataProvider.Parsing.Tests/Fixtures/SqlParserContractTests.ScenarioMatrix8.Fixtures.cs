// Implements [CI-TEST-SHARED-CASE-MATRIX].
namespace Nimblesite.DataProvider.Parsing.Tests;

public partial class SqlParserContractTests
{
    /// <summary>Independent input and expected-value rows for the named scenarios.</summary>
    public static System.Collections.Generic.IEnumerable<object?[]> IndependentScenarioData8()
    {
        {
            yield return
            [
                "Mixed_at_params",
                "SELECT * FROM users WHERE a = @alpha AND b = @beta",
                "alpha",
                "beta",
            ];
        }
        {
            yield return
            [
                "Case_sensitive_params_are_distinct",
                "SELECT * FROM users WHERE id = @Id OR other = @id",
                "Id",
                "id",
            ];
        }
    }
}
