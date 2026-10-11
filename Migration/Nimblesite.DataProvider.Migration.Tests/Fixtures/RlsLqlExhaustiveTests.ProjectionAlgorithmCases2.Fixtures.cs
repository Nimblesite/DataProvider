// Implements [CI-TEST-SHARED-CASE-MATRIX].

namespace Nimblesite.DataProvider.Migration.Tests;

public partial class RlsLqlExhaustiveTests
{
    /// <summary>Independent named scenario inputs and expectations.</summary>
    public static IEnumerable<object?[]> InClause_PassesThrough_ContextCasesData()
    {
        {
            yield return
            [
                "InClause_PassesThrough",
                "status in ('active', 'pending', 'review')",
                "\"status\"",
                "in",
                "'active'",
            ];
        }
        {
            yield return
            [
                "LikeClause_PassesThrough",
                "email like '%@example.com'",
                "\"email\"",
                "like",
                "'%@example.com'",
            ];
        }
    }
}
