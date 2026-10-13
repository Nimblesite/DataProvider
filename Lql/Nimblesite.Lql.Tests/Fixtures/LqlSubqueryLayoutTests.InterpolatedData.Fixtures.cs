// Implements [CI-DESLOP-INDEPENDENT-DATA].
namespace Nimblesite.Lql.Tests;

public partial class LqlSubqueryLayoutTests
{
    private static string SqlData_TwoCtes_JoinedAndPaged_RenderPerDialectPaging_1(
        global::System.String selectPrefix,
        global::System.String tail
    ) =>
        $"""
            WITH active AS (
                SELECT id
                FROM users
                WHERE active = 1
            ),
            big AS (
                SELECT user_id
                FROM orders
                WHERE total > 500
            )
            SELECT {selectPrefix}a.id
            FROM active a
            INNER JOIN big b ON a.id = b.user_id
            {tail}
            """;

    private static string SqlData_DerivedTableJoin_WithOffsetAndLimit_RendersPagingAfterJoin_1(
        global::System.String paging
    ) =>
        $"""
            SELECT u.id
            FROM users u
            INNER JOIN (
                SELECT user_id
                FROM orders
            ) o ON u.id = o.user_id
            {paging}
            """;
}
