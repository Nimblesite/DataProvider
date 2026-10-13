// Implements [CI-TEST-SHARED-CASE-MATRIX].

namespace Nimblesite.Lql.Tests;

public partial class LqlDialectE2ETests
{
    /// <summary>Returns the scenario inputs.</summary>
    public static IEnumerable<object?[]> GroupByWithMultipleAggregates_AllDialects_GeneratesCorrectSQL_CasesInlineCases()
    {
        yield return
        [
            """
                orders
                |> group_by(orders.status)
                |> select(
                    orders.status,
                    count(*) as cnt,
                    sum(orders.total) as total,
                    avg(orders.total) as avg_val,
                    count(distinct orders.user_id) as unique_users
                )
                |> order_by(total desc)
                """,
            "COUNT",
            "SUM",
            "AVG",
            "GROUP BY",
            "ORDER BY",
        ];
        yield return
        [
            """
                orders
                |> select(
                    orders.id,
                    case
                        when orders.total > 1000 then orders.total * 0.95
                        when orders.total > 500 then orders.total * 0.97
                        else orders.total
                    end as discounted
                )
                """,
            "CASE",
            "WHEN",
            "THEN",
            "ELSE",
            "END",
        ];
    }
}
