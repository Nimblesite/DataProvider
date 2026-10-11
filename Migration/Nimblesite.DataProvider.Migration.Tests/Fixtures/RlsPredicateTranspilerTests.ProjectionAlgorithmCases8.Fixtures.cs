// Implements [CI-TEST-SHARED-CASE-MATRIX].

namespace Nimblesite.DataProvider.Migration.Tests;

public partial class RlsPredicateTranspilerTests
{
    /// <summary>Independent named scenario inputs and expectations.</summary>
    public static IEnumerable<object?[]> CurrentUserIdExpression_Postgres_UsesCurrentSetting_ContextCasesData()
    {
        {
            yield return
            [
                "CurrentUserIdExpression_Postgres_UsesCurrentSetting",
                RlsPlatform.Postgres,
                "current_setting('rls.current_user_id', true)",
            ];
        }
        {
            yield return
            [
                "CurrentUserIdExpression_Sqlite_ReadsRlsContextTable",
                RlsPlatform.Sqlite,
                "(SELECT current_user_id FROM [__rls_context] LIMIT 1)",
            ];
        }
        {
            yield return
            [
                "CurrentUserIdExpression_SqlServer_UsesSessionContext",
                RlsPlatform.SqlServer,
                "CAST(SESSION_CONTEXT(N'current_user_id') AS NVARCHAR(450))",
            ];
        }
    }
}
