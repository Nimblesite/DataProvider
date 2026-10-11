// Implements [CI-TEST-SHARED-CASE-MATRIX].

namespace Nimblesite.DataProvider.Parsing.Tests;

public partial class SqlParserContractTests
{
    /// <summary>Independent named scenario inputs and expectations.</summary>
    public static IEnumerable<object?[]> At_user_id_with_underscore_ContextCasesData()
    {
        {
            yield return
            [
                "At_user_id_with_underscore",
                "SELECT * FROM users WHERE id = @user_id",
                "user_id",
            ];
        }
        {
            yield return
            [
                "At_first_name_with_underscore",
                "SELECT * FROM users WHERE first_name = @first_name",
                "first_name",
            ];
        }
    }
}
