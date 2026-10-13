// Implements [CI-TEST-SHARED-CASE-MATRIX].
namespace Nimblesite.DataProvider.Migration.Tests;

public partial class RlsLqlExhaustiveTests
{
    /// <summary>Independent input and expected-value rows for the named scenarios.</summary>
    public static System.Collections.Generic.IEnumerable<object?[]> IndependentScenarioData17()
    {
        {
            yield return
            [
                "OrCombination_SelfOrTenant_TenantMembersShape",
                "user_id = app_user_id() or (tenant_id = app_tenant_id() and is_owner(app_user_id(), app_tenant_id()))",
                "\"user_id\"",
                "\"tenant_id\"",
                "app_user_id()",
                "is_owner(",
            ];
        }
        {
            yield return
            [
                "Parentheses_Nested_PreservedInOutput",
                "(a = 1 and b = 2) or (c = 3 and d = 4)",
                "\"a\"",
                "\"d\"",
                "(",
                ")",
            ];
        }
    }
}
