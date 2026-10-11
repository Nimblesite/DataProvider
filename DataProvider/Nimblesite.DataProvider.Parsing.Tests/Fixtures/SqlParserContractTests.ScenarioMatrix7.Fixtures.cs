// Implements [CI-TEST-SHARED-CASE-MATRIX].
namespace Nimblesite.DataProvider.Parsing.Tests;

public partial class SqlParserContractTests
{
    /// <summary>Independent input and expected-value rows for the named scenarios.</summary>
    public static System.Collections.Generic.IEnumerable<object?[]> IndependentScenarioData7()
    {
        {
            yield return
            [
                "Parses_mixed_case_alias",
                "SELECT name AS UserName FROM users",
                "AS",
                "UserName",
            ];
        }
        {
            yield return
            [
                "Parses_not_exists_subquery",
                "SELECT id FROM users u WHERE NOT EXISTS (SELECT 1 FROM bans b WHERE b.user_id = u.id)",
                "NOT",
                "EXISTS",
            ];
        }
    }
}
