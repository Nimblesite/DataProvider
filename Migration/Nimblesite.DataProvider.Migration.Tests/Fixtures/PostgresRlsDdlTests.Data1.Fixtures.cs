// Implements [CI-DESLOP-INDEPENDENT-DATA].
namespace Nimblesite.DataProvider.Migration.Tests;

public partial class PostgresRlsDdlTests
{
    private const string Data_Generate_CreateRlsPolicy_LqlExistsSubquery_TranspilesAndWraps_1 = """
            UserGroupMemberships
            |> filter(fn(m) => m.user_id = current_user_id())
            |> select(m.user_id)
            """;
}
