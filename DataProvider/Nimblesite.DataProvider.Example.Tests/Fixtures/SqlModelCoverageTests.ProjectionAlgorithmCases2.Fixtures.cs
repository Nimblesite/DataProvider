// Implements [CI-TEST-SHARED-CASE-MATRIX].

namespace Nimblesite.DataProvider.Example.Tests;

public partial class SqlModelCoverageTests
{
    /// <summary>Independent named scenario inputs and expectations.</summary>
    public static IEnumerable<object?[]> FormatValue_WithLongValue_GeneratesNumeric_ContextCasesData()
    {
        {
            yield return ["FormatValue_WithLongValue_GeneratesNumeric", "Count", 42L, "42"];
        }
        {
            yield return
            [
                "FormatValue_WithDecimalValue_GeneratesNumeric",
                "Amount",
                19.99m,
                "19.99",
            ];
        }
    }
}
