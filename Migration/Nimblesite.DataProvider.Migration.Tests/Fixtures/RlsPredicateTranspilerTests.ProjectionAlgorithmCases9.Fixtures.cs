// Implements [CI-TEST-SHARED-CASE-MATRIX].

namespace Nimblesite.DataProvider.Migration.Tests;

public partial class RlsPredicateTranspilerTests
{
    /// <summary>Independent named scenario inputs and expectations.</summary>
    public static IEnumerable<object?[]> Translate_BooleanLiteralPredicate_RoundTripsKeywordsUnquoted_ContextCasesData()
    {
        {
            yield return ["Translate_BooleanLiteralPredicate_RoundTripsKeywordsUnquoted", "any"];
        }
        {
            yield return ["Translate_NapShape_LiteralTrue_PassesThrough", "admin_all"];
        }
    }
}
