// Implements [CI-DESLOP-INDEPENDENT-DATA].
namespace Nimblesite.Lql.Tests;

public partial class LqlFileBasedTests
{
    private const string Data_LargeQuery_TranspilesCompleteShapeForEveryDialect_1 = """
        -- Complex query with multiple operations
        let base_users =
            users
            |> join(user_profiles, on = users.id = user_profiles.user_id)
            |> join(user_settings, on = users.id = user_settings.user_id)
            |> filter(fn(row) => row.users.status = 'active' and row.user_profiles.verified = true)

        let user_orders =
            base_users
            |> join(orders, on = users.id = orders.user_id)
            |> join(order_items, on = orders.id = order_items.order_id)
            |> join(products, on = order_items.product_id = products.id)
            |> join(categories, on = products.category_id = categories.id)

        let aggregated_data =
            user_orders
            |> group_by(users.id, users.name, categories.name)
            |> select(
                users.id,
                users.name,
                categories.name as category_name,
                count(distinct orders.id) as order_count,
                sum(order_items.quantity * products.price) as total_spent,
                avg(products.rating) as avg_product_rating
            )
            |> having(fn(group) => count(distinct orders.id) > 5)

        aggregated_data
        |> filter(fn(row) => row.total_spent > 1000)
        |> order_by(total_spent desc, order_count desc)
        |> limit(100)
        """;
}
