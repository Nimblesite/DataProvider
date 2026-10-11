// Implements [CI-DESLOP-INDEPENDENT-DATA].
namespace Nimblesite.Lql.Tests;

public partial class LqlDialectE2ETests
{
    private const string Data_CountFunction_AllDialects_GeneratesCorrectSQL_1 = """
            orders
            |> group_by(orders.user_id)
            |> select(orders.user_id, count(*) as order_count)
            """;

    private const string Data_SumAndAvgFunctions_AllDialects_GeneratesCorrectSQL_1 = """
            orders
            |> group_by(orders.status)
            |> select(
                orders.status,
                sum(orders.total) as total_amount,
                avg(orders.total) as avg_amount
            )
            """;

    private const string Data_CountDistinctFunction_AllDialects_GeneratesCorrectSQL_1 = """
            orders
            |> group_by(orders.user_id)
            |> select(orders.user_id, count(distinct orders.product_id) as unique_products)
            """;

    private const string Data_OffsetAndLimit_AllDialects_GeneratesCorrectPagination_1 = """
            users
            |> order_by(users.name asc)
            |> offset(20)
            |> limit(10)
            |> select(users.id, users.name)
            """;

    private const string Data_HavingClause_AllDialects_GeneratesCorrectSQL_1 = """
            orders
            |> group_by(orders.user_id)
            |> having(fn(group) => count(*) > 5)
            |> select(orders.user_id, count(*) as order_count)
            """;

    private const string Data_LeftJoin_AllDialects_GeneratesLeftJoinSQL_1 = """
            users
            |> left_join(orders, on = users.id = orders.user_id)
            |> select(users.name, orders.total)
            """;

    private const string Data_MultipleJoins_AllDialects_GeneratesCorrectSQL_1 = """
            users
            |> join(orders, on = users.id = orders.user_id)
            |> join(products, on = orders.product_id = products.id)
            |> select(users.name, orders.total, products.name)
            """;
}
