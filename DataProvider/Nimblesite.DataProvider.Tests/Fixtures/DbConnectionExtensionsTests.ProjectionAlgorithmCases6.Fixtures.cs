// Implements [CI-TEST-SHARED-CASE-MATRIX].

namespace Nimblesite.DataProvider.Tests;

public partial class DbConnectionExtensionsTests
{
    /// <summary>Independent named scenario inputs and expectations.</summary>
    public static IEnumerable<object?[]> Execute_WithEmptySql_ReturnsError_ContextCasesData()
    {
        {
            yield return ["Execute_WithEmptySql_ReturnsError", "   "];
        }
        {
            yield return
            [
                "Execute_WithInvalidSql_ReturnsError",
                "INSERT INTO NonExistentTable (Name) VALUES ('Test')",
            ];
        }
    }
}
