// Implements [CI-DESLOP-INDEPENDENT-DATA].
namespace Nimblesite.DataProvider.Migration.Tests;

public partial class RlsLqlExhaustiveTests
{
    private const string Data_ExistsSubquery_GroupMembership_TranspilesViaLqlEngine_1 = """
            users
            |> filter(fn(u) => u.id = current_user_id())
            |> select(users.id)
            """;

    private const string Data_ExistsPipeline_LambdaWithFnCallInAndClause_Parses_1 = """
            conversations
            |> filter(fn(p) => p.id = '00000000-0000-0000-0000-000000000000' and is_member('a', p.tenant_id))
            |> select(p.id)
            """;

    private const string Data_ExistsPipeline_LambdaScope_StripsLambdaVarFromQualifiedRefs_1 = """
            conversations
            |> filter(fn(c) => c.id = '00000000-0000-0000-0000-000000000000' and is_member('a', c.tenant_id))
            |> select(c.id)
            """;

    private const string Data_ExistsPipeline_LambdaWithSecurityDefinerFnAtTopLevel_Parses_1 = """
            conversations
            |> filter(fn(p) => is_member('a', p.tenant_id))
            |> select(p.id)
            """;
}
