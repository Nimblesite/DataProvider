// Implements [CI-TEST-SHARED-CASE-MATRIX].

namespace Nimblesite.DataProvider.Tests;

public partial class DbTransactTests
{
    /// <summary>Independent named scenario inputs and expectations.</summary>
    public static IEnumerable<object?[]> Transact_SqliteConnection_ThrowsOnNullBody_ContextCasesData()
    {
        {
            yield return ["Transact_SqliteConnection_ThrowsOnNullBody"];
        }
        {
            yield return ["Transact_SqliteConnection_WithReturnValue_ThrowsOnNullBody"];
        }
    }
}
