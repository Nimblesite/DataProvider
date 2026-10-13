// Implements [CI-TEST-SHARED-CASE-MATRIX].

namespace Nimblesite.DataProvider.Example.Tests;

public partial class SqlModelCoverageTests
{
    /// <summary>Independent named scenario inputs and expectations.</summary>
    public static IEnumerable<object?[]> SqlError_Create_SetsMessageCorrectly_ContextCasesData()
    {
        {
            yield return
            [
                "SqlError_Create_SetsMessageCorrectly",
                "Test error message",
                "Test error message",
                new global::System.Func<
                    global::Nimblesite.Sql.Model.SqlError,
                    global::System.String
                >(projection => projection.Message),
            ];
        }
        {
            yield return
            [
                "SqlError_FormattedMessage_WithoutPosition_ReturnsMessage",
                "Simple error",
                "Simple error",
                new global::System.Func<
                    global::Nimblesite.Sql.Model.SqlError,
                    global::System.String
                >(projection => projection.FormattedMessage),
            ];
        }
    }
}
