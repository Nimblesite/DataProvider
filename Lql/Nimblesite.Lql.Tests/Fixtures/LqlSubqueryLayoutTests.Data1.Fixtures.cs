// Implements [CI-DESLOP-INDEPENDENT-DATA].
namespace Nimblesite.Lql.Tests;

public partial class LqlSubqueryLayoutTests
{
    private const string Data_ExistsBody_WithTwoFilters_KeepsBothAndGroupsEach_1 = """
        SELECT users.id
        FROM users
        WHERE EXISTS (
            SELECT 1
            FROM orders
            WHERE (orders.user_id = users.id) AND (orders.total > 100 OR orders.vip = 1)
        )
        """;
}
