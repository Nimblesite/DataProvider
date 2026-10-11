// Implements [CI-TEST-SHARED-CASE-MATRIX].
namespace Nimblesite.DataProvider.Migration.Tests;

public partial class RlsPredicateTranspilerTests
{
    public static IEnumerable<object?[]> Translate_SimplePredicate_Postgres_QuotesColumnAndSubstitutesUserId_CasesData()
    {
        {
            yield return
            [
                "Translate_SimplePredicate_Postgres_QuotesColumnAndSubstitutesUserId",
                RlsPlatform.Postgres,
                "\"OwnerId\"",
                "current_setting('rls.current_user_id', true)",
            ];
        }
        {
            yield return
            [
                "Translate_SimplePredicate_Sqlite_BracketsColumn",
                RlsPlatform.Sqlite,
                "[OwnerId]",
                "__rls_context",
            ];
        }
        {
            yield return
            [
                "Translate_SimplePredicate_SqlServer_BracketsColumnAndUsesSessionContext",
                RlsPlatform.SqlServer,
                "[OwnerId]",
                "SESSION_CONTEXT",
            ];
        }
    }
}
