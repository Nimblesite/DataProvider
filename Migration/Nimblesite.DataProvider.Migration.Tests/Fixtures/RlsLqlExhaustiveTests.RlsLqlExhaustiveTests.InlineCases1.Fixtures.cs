// Implements [CI-TEST-SHARED-CASE-MATRIX].

namespace Nimblesite.DataProvider.Migration.Tests;

public partial class RlsLqlExhaustiveTests
{
    /// <summary>Returns the scenario inputs.</summary>
    public static IEnumerable<object?[]> AndCombination_TwoPredicates_QuotesColumns_Postgres_CasesInlineCases()
    {
        yield return
        [
            "tenant_id = app_tenant_id() and is_member(app_user_id(), app_tenant_id())",
            "\"tenant_id\"",
            "is_member(",
        ];
        yield return ["status = 'active'", "'active'", "\"status\""];
        yield return ["balance > -5", "\"balance\"", "-5"];
        yield return
        [
            """
                tenant_id = app_tenant_id()
                  and is_member(app_user_id(), app_tenant_id())
                """,
            "\"tenant_id\"",
            "is_member(",
        ];
        yield return ["tenant_id   =    app_tenant_id()", "\"tenant_id\"", "app_tenant_id()"];
    }

    /// <summary>Returns the scenario inputs.</summary>
    public static IEnumerable<object?[]> ComparisonOperators_Survive_PostgresInlineCases()
    {
        yield return ["="];
        yield return ["<>"];
        yield return ["!="];
        yield return [">"];
        yield return [">="];
        yield return ["<"];
        yield return ["<="];
    }

    /// <summary>Returns the scenario inputs.</summary>
    public static IEnumerable<object?[]> MixedCaseIdentifier_QuotedPreservesCase_CasesInlineCases()
    {
        yield return ["OwnerId = current_user_id()", "\"OwnerId\""];
        yield return ["_internal_flag = true", "\"_internal_flag\""];
        yield return ["col1 = 'x'", "\"col1\""];
    }
}
