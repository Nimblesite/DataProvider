// Implements [CI-TEST-SHARED-CASE-MATRIX].
namespace Nimblesite.DataProvider.Parsing.Tests;

public partial class SqlParserContractTests
{
    /// <summary>Independent input and expected-value rows for the named scenarios.</summary>
    public static System.Collections.Generic.IEnumerable<object?[]> IndependentScenarioData6()
    {
        {
            yield return
            [
                "Parses_inner_join",
                "SELECT a.id, b.name FROM a JOIN b ON a.b_id = b.id",
                "JOIN",
            ];
        }
        {
            yield return ["Parses_function_call_with_wildcard", "SELECT count(*) FROM users", "*"];
        }
    }
}
