using Nimblesite.Lql.Postgres;
using Nimblesite.Lql.SQLite;
using Nimblesite.Lql.SqlServer;
using Nimblesite.Sql.Model;
using Outcome;
using Xunit;
using LqlStatementError = Outcome.Result<
    Nimblesite.Lql.Core.LqlStatement,
    Nimblesite.Sql.Model.SqlError
>.Error<Nimblesite.Lql.Core.LqlStatement, Nimblesite.Sql.Model.SqlError>;
using LqlStatementOk = Outcome.Result<
    Nimblesite.Lql.Core.LqlStatement,
    Nimblesite.Sql.Model.SqlError
>.Ok<Nimblesite.Lql.Core.LqlStatement, Nimblesite.Sql.Model.SqlError>;
using SqlTextOk = Outcome.Result<string, Nimblesite.Sql.Model.SqlError>.Ok<
    string,
    Nimblesite.Sql.Model.SqlError
>;

namespace Nimblesite.Lql.Tests;

/// <summary>
/// CTEs, derived-table joins and EXISTS/IN bodies share one layout on every dialect;
/// only paging differs. Tests [LQL-CTE], [LQL-DERIVED-TABLE] and [LQL-SUBQUERY-LAYOUT].
/// </summary>
public sealed class LqlSubqueryLayoutTests
{
    [Theory]
    [InlineData("SQLite")]
    [InlineData("PostgreSql")]
    [InlineData("SqlServer")]
    public void ExistsBody_WithTwoFilters_KeepsBothAndGroupsEach(string dialect) =>
        Assert.Equal(
            """
            SELECT users.id
            FROM users
            WHERE EXISTS (
                SELECT 1
                FROM orders
                WHERE (orders.user_id = users.id) AND (orders.total > 100 OR orders.vip = 1)
            )
            """,
            Transpile(
                "users |> filter(fn(row) => exists(orders |> filter(fn(o) => o.orders.user_id = row.users.id) |> filter(fn(o) => o.orders.total > 100 or o.orders.vip = 1))) |> select(users.id)",
                dialect
            )
        );

    [Theory]
    [InlineData("SQLite", "", "ORDER BY a.id ASC\nLIMIT 5")]
    [InlineData("PostgreSql", "", "ORDER BY a.id ASC\nLIMIT 5")]
    [InlineData("SqlServer", "TOP 5 ", "ORDER BY a.id ASC")]
    public void TwoCtes_JoinedAndPaged_RenderPerDialectPaging(
        string dialect,
        string selectPrefix,
        string tail
    ) =>
        Assert.Equal(
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
            """,
            Transpile(
                "with active as (users |> filter(fn(u) => u.users.active = 1) |> select(users.id)), big as (orders |> filter(fn(o) => o.orders.total > 500) |> select(orders.user_id)) active |> join(big, on = active.id = big.user_id) |> select(active.id) |> order_by(active.id asc) |> limit(5)",
                dialect
            )
        );

    [Theory]
    [InlineData("SQLite", "LIMIT 5\nOFFSET 10")]
    [InlineData("PostgreSql", "LIMIT 5\nOFFSET 10")]
    [InlineData("SqlServer", "OFFSET 10 ROWS\nFETCH NEXT 5 ROWS ONLY")]
    public void DerivedTableJoin_WithOffsetAndLimit_RendersPagingAfterJoin(
        string dialect,
        string paging
    ) =>
        Assert.Equal(
            $"""
            SELECT u.id
            FROM users u
            INNER JOIN (
                SELECT user_id
                FROM orders
            ) o ON u.id = o.user_id
            {paging}
            """,
            Transpile(
                "users |> join((orders |> select(orders.user_id)), on = users.id = orders.user_id) |> select(users.id) |> offset(10) |> limit(5)",
                dialect
            )
        );

    [Fact]
    public void ExistsBody_WithLimit_IsRejectedInsteadOfDropped()
    {
        var error = Assert.IsType<LqlStatementError>(
            LqlStatementConverter.ToStatement(
                "users |> filter(fn(row) => exists(orders |> limit(1))) |> select(users.id)"
            )
        );
        Assert.Contains(
            "limit/offset are not supported inside EXISTS or IN subqueries",
            error.Value.Message,
            StringComparison.Ordinal
        );
    }

    private static string Transpile(string lql, string dialect)
    {
        var statement = Assert.IsType<LqlStatementOk>(LqlStatementConverter.ToStatement(lql)).Value;
        Result<string, SqlError> sql = dialect switch
        {
            "PostgreSql" => statement.ToPostgreSql(),
            "SqlServer" => statement.ToSqlServer(),
            _ => statement.ToSQLite(),
        };
        return Assert.IsType<SqlTextOk>(sql).Value;
    }
}
