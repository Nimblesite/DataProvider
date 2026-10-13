// Implements [CI-TEST-SHARED-CASE-MATRIX].

namespace Nimblesite.DataProvider.Tests;

public partial class DummyParameterValuesTests
{
    /// <summary>Returns the scenario inputs.</summary>
    public static IEnumerable<object?[]> Integer_names_return_intInlineCases()
    {
        yield return ["userId", typeof(int)];
        yield return ["id", typeof(int)];
        yield return ["USER_ID", typeof(int)];
        yield return ["limit", typeof(int)];
        yield return ["LIMIT", typeof(int)];
        yield return ["offset", typeof(int)];
        yield return ["count", typeof(int)];
        yield return ["row_count", typeof(int)];
        yield return ["quantity", typeof(int)];
    }

    /// <summary>Returns the scenario inputs.</summary>
    public static IEnumerable<object?[]> Decimal_names_return_decimalInlineCases()
    {
        yield return ["amount"];
        yield return ["price"];
        yield return ["total"];
        yield return ["percentage"];
        yield return ["unit_price"];
    }

    /// <summary>Returns the scenario inputs.</summary>
    public static IEnumerable<object?[]> Temporal_names_return_datetimeInlineCases()
    {
        yield return ["date"];
        yield return ["startDate"];
        yield return ["time"];
        yield return ["created"];
        yield return ["updated"];
    }

    /// <summary>Returns the scenario inputs.</summary>
    public static IEnumerable<object?[]> Id_match_returns_int_one_CasesInlineCases()
    {
        yield return ["id", 1];
        yield return ["limit", 100];
        yield return ["offset", 0];
    }
}
