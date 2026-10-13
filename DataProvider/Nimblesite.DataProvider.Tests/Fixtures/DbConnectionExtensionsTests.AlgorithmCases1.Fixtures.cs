// Implements [CI-TEST-SHARED-CASE-MATRIX].
namespace Nimblesite.DataProvider.Tests;

public partial class DbConnectionExtensionsTests
{
    public static IEnumerable<object?[]> Query_WithEmptySql_ReturnsError_CasesData()
    {
        {
            yield return ["Query_WithEmptySql_ReturnsError", "   "];
        }
        {
            yield return ["Query_WithInvalidSql_ReturnsError", "SELECT * FROM NonExistentTable"];
        }
    }
}
