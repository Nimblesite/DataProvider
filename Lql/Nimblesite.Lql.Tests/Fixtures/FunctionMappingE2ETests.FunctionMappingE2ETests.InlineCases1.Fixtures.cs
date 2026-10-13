// Implements [CI-TEST-SHARED-CASE-MATRIX].

namespace Nimblesite.Lql.Tests;

public partial class FunctionMappingE2ETests
{
    /// <summary>Returns the scenario inputs.</summary>
    public static IEnumerable<object?[]> CoreAggregateFunctions_AllProviders_AreMappedInlineCases()
    {
        yield return ["count"];
        yield return ["sum"];
        yield return ["avg"];
        yield return ["min"];
        yield return ["max"];
        yield return ["coalesce"];
    }

    /// <summary>Returns the scenario inputs.</summary>
    public static IEnumerable<object?[]> StringFunctions_AllProviders_AreMappedInlineCases()
    {
        yield return ["upper"];
        yield return ["lower"];
    }

    /// <summary>Returns the scenario inputs.</summary>
    public static IEnumerable<object?[]> CountWithColumn_AllProviders_ProducesCorrectSQL_CasesInlineCases()
    {
        yield return ["count", "user_id", "COUNT", "user_id"];
        yield return ["sum", "amount", "SUM", "amount"];
        yield return ["upper", "name", "UPPER", "name"];
        yield return ["lower", "email", "LOWER", "email"];
        yield return ["avg", "rating", "AVG", "rating"];
    }
}
