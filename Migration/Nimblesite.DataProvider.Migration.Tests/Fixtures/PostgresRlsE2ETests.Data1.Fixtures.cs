// Implements [CI-DESLOP-INDEPENDENT-DATA].
namespace Nimblesite.DataProvider.Migration.Tests;

public partial class PostgresRlsE2ETests
{
    private const string Data_BuildGroupMembershipSchema_1 = """
        exists(
          user_group_memberships
          |> filter(fn(m) => m.user_id = current_user_id() and m.group_id = documents.group_id)
          |> select(id)
        )
        """;

    private const string Data_EnableRls_TableHasRlsEnabled_1 = """
        SELECT c.relrowsecurity
        FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
        WHERE n.nspname = 'public' AND c.relname = 'documents'
        """;
}
