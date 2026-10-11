// Implements [CI-TEST-SHARED-CASE-MATRIX].

namespace Nimblesite.Lql.Tests;

public partial class LqlDialectE2ETests
{
    /// <summary>Independent named scenario inputs and expectations.</summary>
    public static IEnumerable<object?[]> SimpleTableReference_AllDialects_GeneratesSelectStar_ContextCasesData()
    {
        {
            yield return
            [
                "SimpleTableReference_AllDialects_GeneratesSelectStar",
                "users |> select(users.id, users.name)",
                "SELECT",
                "FROM",
            ];
        }
        {
            yield return
            [
                "SelectDistinct_AllDialects_GeneratesDistinctSQL",
                """
                    users |> select_distinct(users.country)
                    """,
                "SELECT DISTINCT",
                "country",
            ];
        }
    }
}
