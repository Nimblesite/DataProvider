// Implements [CI-TEST-SHARED-CASE-MATRIX].

namespace Nimblesite.DataProvider.Tests;

public partial class DbTransactionExtensionsTests
{
    /// <summary>Independent named scenario inputs and expectations.</summary>
    public static IEnumerable<object?[]> Scalar_WithEmptySql_ReturnsError_ContextCasesData()
    {
        {
            yield return ["Scalar_WithEmptySql_ReturnsError", "   "];
        }
        {
            yield return
            [
                "Scalar_WithInvalidSql_ReturnsError",
                "SELECT Name FROM NonExistentTable",
            ];
        }
    }
}
