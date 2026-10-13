// Implements [CI-TEST-SHARED-CASE-MATRIX].
namespace Nimblesite.Lql.Tests;

public partial class LqlFnCallInLambdaTests
{
    /// <summary>Independent named inputs for the shared scenario runner.</summary>
    public static System.Collections.Generic.IEnumerable<object?[]> IndependentCommentScenarioData1()
    {
        {
            yield return
            [
                "Lambda_BareFnCall_NestedFnCallArg_PassesThrough",
                "t |> filter(fn(x) => is_member(app_user_id(), app_tenant_id()))",
                "is_member(",
                "APP_USER_ID()",
                "APP_TENANT_ID()",
            ];
        }
        {
            yield return
            [
                "Lambda_AndCombinationWithBareFnCall_ParsesAndEmits",
                "t |> filter(fn(x) => x.id = '00000000-0000-0000-0000-000000000000' and is_member('u', x.tenant_id))",
                "AND",
                "is_member(",
                "00000000-0000-0000-0000-000000000000",
            ];
        }
        {
            yield return
            [
                "Lambda_OrCombinationWithBareFnCall_ParsesAndEmits",
                "t |> filter(fn(x) => x.id = '00000000-0000-0000-0000-000000000000' or is_member('u', x.tenant_id))",
                "OR",
                "is_member(",
                "00000000-0000-0000-0000-000000000000",
            ];
        }
    }
}
