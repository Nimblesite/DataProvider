// Implements [CI-DESLOP-INDEPENDENT-DATA].
namespace Nimblesite.Lql.Tests;

public partial class LqlDialectE2ETests
{
    private const string Data_ComplexFilterWithAndOr_AllDialects_GeneratesCorrectSQL_1 = """
            users
            |> filter(fn(row) => row.users.age > 18 and row.users.country = 'US' or row.users.status = 'premium')
            |> select(users.id, users.name)
            """;

    private const string Data_OrderByDescending_AllDialects_GeneratesDescSQL_1 = """
            users
            |> order_by(users.age desc, users.name asc)
            |> select(users.id, users.name, users.age)
            """;

    private const string Data_FilterWithStringLiteral_AllDialects_PreservesQuotes_1 = """
            users
            |> filter(fn(row) => row.users.status = 'active')
            |> select(users.id, users.name)
            """;

    private const string Data_FilterWithNumericComparison_AllDialects_GeneratesCorrectSQL_1 = """
            products
            |> filter(fn(row) => row.products.price >= 100 and row.products.price <= 500)
            |> select(products.id, products.name, products.price)
            """;

    private const string Data_JoinWithFilterAndOrderBy_AllDialects_FullPipelineWorks_1 = """
            users
            |> join(orders, on = users.id = orders.user_id)
            |> filter(fn(row) => row.orders.total > 100)
            |> order_by(orders.total desc)
            |> limit(50)
            |> select(users.name, orders.total, orders.status)
            """;

    private const string Data_ArithmeticExpressions_AllDialects_GeneratesCorrectSQL_1 = """
            products
            |> select(
                products.id,
                products.price * products.quantity as total_value,
                products.price + 10 as price_plus_ten
            )
            """;

    private const string Data_FilterWithNotEquals_AllDialects_GeneratesCorrectOperator_1 = """
            users
            |> filter(fn(row) => row.users.status != 'deleted')
            |> select(users.id, users.name)
            """;
}
