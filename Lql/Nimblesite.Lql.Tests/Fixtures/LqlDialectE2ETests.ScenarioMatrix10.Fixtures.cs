// Implements [CI-TEST-SHARED-CASE-MATRIX].
namespace Nimblesite.Lql.Tests;

public partial class LqlDialectE2ETests
{
    /// <summary>Independent input and expected-value rows for the named scenarios.</summary>
    public static System.Collections.Generic.IEnumerable<object?[]> IndependentScenarioData10()
    {
        {
            yield return
            [
                "ComplexFilterWithAndOr_AllDialects_GeneratesCorrectSQL",
                Data_ComplexFilterWithAndOr_AllDialects_GeneratesCorrectSQL_1,
                "WHERE",
                "AND",
                "OR",
            ];
        }
        {
            yield return
            [
                "OrderByDescending_AllDialects_GeneratesDescSQL",
                Data_OrderByDescending_AllDialects_GeneratesDescSQL_1,
                "ORDER BY",
                "DESC",
                "ASC",
            ];
        }
    }
}
