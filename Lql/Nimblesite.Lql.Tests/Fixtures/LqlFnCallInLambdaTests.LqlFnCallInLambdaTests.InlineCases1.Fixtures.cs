// Implements [CI-TEST-SHARED-CASE-MATRIX].

namespace Nimblesite.Lql.Tests;

public partial class LqlFnCallInLambdaTests
{
    /// <summary>Returns the scenario inputs.</summary>
    public static IEnumerable<object?[]> Lambda_BareFnCall_StringArgs_PassesThrough_CasesInlineCases()
    {
        yield return
        [
            "t |> filter(fn(x) => is_member('a', 'b'))",
            "is_member('a', 'b')",
            "is_member()",
        ];
        yield return ["t |> filter(fn(x) => has_role(42))", "has_role(42)", "has_role()"];
        yield return
        [
            "t |> filter(fn(x) => has_balance(1.5))",
            "has_balance(1.5)",
            "has_balance()",
        ];
    }

    /// <summary>Returns the scenario inputs.</summary>
    public static IEnumerable<object?[]> InList_ChangingMembership_PreservesExactPredicateAcrossDialectsInlineCases()
    {
        yield return ["'owner', 'admin'", "'owner', 'admin', 'auditor'"];
        yield return ["1, 2", "1, 2, 3"];
    }
}
