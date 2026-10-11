// Implements [CI-DESLOP-INDEPENDENT-DATA].
namespace Nimblesite.DataProvider.Migration.Tests;

public partial class RlsPredicateTranspilerTests
{
    private const string Data_Translate_ExistsSubquery_Postgres_WrapsWithExistsAndQuotesIdentifiers_1 = """
            users
            |> filter(fn(u) => u.id = current_user_id())
            |> select(users.id)
            """;

    private const string Data_Translate_ExistsSubquery_Sqlite_SubstitutesContextLookup_1 = """
            users
            |> filter(fn(u) => u.id = current_user_id())
            |> select(users.id)
            """;
}
