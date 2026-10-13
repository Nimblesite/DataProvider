// Implements [CI-DESLOP-INDEPENDENT-DATA].
namespace Nimblesite.DataProvider.Migration.Tests;

public partial class PostgresSupportE2ETests
{
    private static string SqlData_MembershipFunction_1(global::System.String roleClause) =>
        $"""
            SELECT EXISTS (
                SELECT 1
                FROM public.tenant_members tm
                WHERE tm.tenant_id = p_tenant_id
                  AND tm.user_id = p_user_id
                  {roleClause}
            )
            """;
}
