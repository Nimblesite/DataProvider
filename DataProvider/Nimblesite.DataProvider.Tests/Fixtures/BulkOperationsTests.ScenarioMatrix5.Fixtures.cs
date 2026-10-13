// Implements [CI-TEST-SHARED-CASE-MATRIX].
namespace Nimblesite.DataProvider.Tests;

public partial class BulkOperationsTests
{
    /// <summary>Independent input and expected-value rows for the named scenarios.</summary>
    public static System.Collections.Generic.IEnumerable<object?[]> IndependentScenarioData5()
    {
        {
            yield return
            [
                "GenerateBulkInsertMethod_HandlesNullableTypes",
                "?? (object)DBNull.Value",
            ];
        }
        {
            yield return
            [
                "GenerateBulkInsertMethod_UsesExtensionMethodPattern",
                "this IDbTransaction transaction",
            ];
        }
    }
}
