// Implements [CI-TEST-SHARED-CASE-MATRIX].

namespace Nimblesite.DataProvider.Migration.Tests;

public partial class RlsLqlExhaustiveTests
{
    /// <summary>Independent named scenario inputs and expectations.</summary>
    public static IEnumerable<object?[]> CustomGucReader_AppUserId_PassesThrough_Postgres_ContextCasesData()
    {
        {
            yield return
            [
                "CustomGucReader_AppUserId_PassesThrough_Postgres",
                "user_id = app_user_id()",
                "app_user_id()",
                "\"app_user_id\"",
            ];
        }
        {
            yield return
            [
                "StringLiteralWithReservedWordsInside_NotIdentifierQuoted",
                "name = 'AND OR NOT'",
                "'AND OR NOT'",
                "\"AND\"",
            ];
        }
    }
}
