// Implements [CI-TEST-SHARED-CASE-MATRIX].

namespace Nimblesite.DataProvider.Example.Tests;

public partial class SqlModelCoverageTests
{
    /// <summary>Independent named scenario inputs and expectations.</summary>
    public static IEnumerable<object?[]> Builder_WhereWithBoolFalse_GeneratesZero_ContextCasesData()
    {
        {
            yield return
            [
                "Builder_WhereWithBoolFalse_GeneratesZero",
                "o.IsActive",
                false,
                "o.IsActive = 0",
            ];
        }
        {
            yield return
            [
                "Builder_WhereWithDoubleValue_GeneratesNumeric",
                "o.TotalAmount",
                99.99,
                "99.99",
            ];
        }
    }
}
