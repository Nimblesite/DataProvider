// Implements [CI-TEST-SHARED-CASE-MATRIX].
namespace Nimblesite.DataProvider.Migration.Tests;

public partial class RlsPredicateTranspilerTests
{
    /// <summary>Independent input and expected-value rows for the named scenarios.</summary>
    public static System.Collections.Generic.IEnumerable<object?[]> IndependentScenarioData18()
    {
        {
            yield return
            [
                "Translate_EmptyPredicate_ReturnsRlsEmptyPredicateError",
                "",
                "p",
                "MIG-E-RLS-EMPTY-PREDICATE",
                "'p'",
            ];
        }
        {
            yield return
            [
                "Translate_ExistsSubquery_BadLql_ReturnsLqlParseError",
                "exists(this is not valid lql @@@)",
                "broken",
                "MIG-E-RLS-LQL",
                "'broken'",
            ];
        }
    }
}
