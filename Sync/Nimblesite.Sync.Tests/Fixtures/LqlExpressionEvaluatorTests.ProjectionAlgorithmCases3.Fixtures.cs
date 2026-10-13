// Implements [CI-TEST-SHARED-CASE-MATRIX].

namespace Nimblesite.Sync.Tests;

public partial class LqlExpressionEvaluatorTests
{
    /// <summary>Independent named scenario inputs and expectations.</summary>
    public static IEnumerable<object?[]> Length_ReturnsStringLength_ContextCasesData()
    {
        {
            yield return ["Length_ReturnsStringLength", """{"Name":"Alice"}""", "length(Name)", 5];
        }
        {
            yield return
            [
                "LqlTransform_NumericColumn_Works",
                """{"Price":99.99}""",
                "Price",
                99.99,
            ];
        }
        {
            yield return
            [
                "LqlTransform_BooleanColumn_Works",
                """{"IsActive":true}""",
                "IsActive",
                true,
            ];
        }
    }
}
