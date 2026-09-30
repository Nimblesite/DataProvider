using Nimblesite.Lql.Postgres;
using Nimblesite.Lql.SQLite;
using Nimblesite.Lql.SqlServer;
using Nimblesite.Sql.Model;
using Outcome;
using Xunit;

namespace Nimblesite.Lql.Tests;

/// <summary>
/// E2E tests: LQL parse -> convert to all 3 SQL dialects -> verify output.
/// Targets function mappings, DISTINCT, complex filters, and edge cases.
/// </summary>
public sealed partial class LqlDialectE2ETests
{
    // Implements [LQL-OUTPUT-DIALECTS].
    private static (string PostgreSql, string SqlServer, string SQLite) ConvertToAllDialects(
        string lqlCode
    )
    {
        var result = LqlStatementConverter.ToStatement(lqlCode);
        if (result is not Result<LqlStatement, SqlError>.Ok<LqlStatement, SqlError> parseOk)
        {
            Assert.Fail(
                $"Parse failed: {((Result<LqlStatement, SqlError>.Error<LqlStatement, SqlError>)result).Value.DetailedMessage}"
            );
            return default;
        }

        var stmt = parseOk.Value;

        var pg = stmt.ToPostgreSql();
        var ss = stmt.ToSqlServer();
        var sl = stmt.ToSQLite();

        if (pg is not Result<string, SqlError>.Ok<string, SqlError> pgOk)
        {
            Assert.Fail("PostgreSql conversion failed");
            return default;
        }

        if (ss is not Result<string, SqlError>.Ok<string, SqlError> ssOk)
        {
            Assert.Fail("SqlServer conversion failed");
            return default;
        }

        if (sl is not Result<string, SqlError>.Ok<string, SqlError> slOk)
        {
            Assert.Fail("SQLite conversion failed");
            return default;
        }

        return (pgOk.Value, ssOk.Value, slOk.Value);
    }

    [Fact]
    public void CountFunction_AllDialects_GeneratesCorrectSQL()
    {
        var lql = """
            orders
            |> group_by(orders.user_id)
            |> select(orders.user_id, count(*) as order_count)
            """;
        var (pg, ss, sl) = ConvertToAllDialects(lql);

        Assert.Contains("COUNT(*)", pg, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("COUNT(*)", ss, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("COUNT(*)", sl, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("GROUP BY", pg, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("GROUP BY", ss, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("GROUP BY", sl, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("order_count", pg, StringComparison.Ordinal);
        Assert.Contains("order_count", ss, StringComparison.Ordinal);
        Assert.Contains("order_count", sl, StringComparison.Ordinal);
    }

    [Fact]
    public void SumAndAvgFunctions_AllDialects_GeneratesCorrectSQL()
    {
        var lql = """
            orders
            |> group_by(orders.status)
            |> select(
                orders.status,
                sum(orders.total) as total_amount,
                avg(orders.total) as avg_amount
            )
            """;
        var (pg, ss, sl) = ConvertToAllDialects(lql);

        foreach (var sql in new[] { pg, ss, sl })
        {
            Assert.Contains("SUM", sql, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("AVG", sql, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("total_amount", sql, StringComparison.Ordinal);
            Assert.Contains("avg_amount", sql, StringComparison.Ordinal);
            Assert.Contains("GROUP BY", sql, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void CountDistinctFunction_AllDialects_GeneratesCorrectSQL()
    {
        var lql = """
            orders
            |> group_by(orders.user_id)
            |> select(orders.user_id, count(distinct orders.product_id) as unique_products)
            """;
        var (pg, ss, sl) = ConvertToAllDialects(lql);

        foreach (var sql in new[] { pg, ss, sl })
        {
            Assert.Contains("COUNT", sql, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("DISTINCT", sql, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("COUNT(DISTINCT", sql, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("product_id", sql, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("GROUP BY", sql, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("unique_products", sql, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void UpperLowerFunctions_AllDialects_GeneratesCorrectSQL()
    {
        var lql = """
            users
            |> select(upper(users.name) as upper_name, lower(users.email) as lower_email)
            """;
        var (pg, ss, sl) = ConvertToAllDialects(lql);

        foreach (var sql in new[] { pg, ss, sl })
        {
            Assert.Contains("UPPER", sql, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("LOWER", sql, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("upper_name", sql, StringComparison.Ordinal);
            Assert.Contains("lower_email", sql, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void SelectDistinct_AllDialects_GeneratesDistinctSQL()
    {
        var lql = """
            users |> select_distinct(users.country)
            """;
        var (pg, ss, sl) = ConvertToAllDialects(lql);

        foreach (var sql in new[] { pg, ss, sl })
        {
            Assert.Contains("SELECT DISTINCT", sql, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("country", sql, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void OffsetAndLimit_AllDialects_GeneratesCorrectPagination()
    {
        var lql = """
            users
            |> order_by(users.name asc)
            |> offset(20)
            |> limit(10)
            |> select(users.id, users.name)
            """;
        var (pg, ss, sl) = ConvertToAllDialects(lql);

        Assert.Contains("OFFSET", pg, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("LIMIT", pg, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("20", pg, StringComparison.Ordinal);
        Assert.Contains("10", pg, StringComparison.Ordinal);
        Assert.Contains("OFFSET", sl, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("LIMIT", sl, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("20", sl, StringComparison.Ordinal);
        Assert.Contains("10", sl, StringComparison.Ordinal);
        // SQL Server uses OFFSET...FETCH or TOP
        Assert.Contains("OFFSET", ss, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("20", ss, StringComparison.Ordinal);
        Assert.Contains("10", ss, StringComparison.Ordinal);
        Assert.True(
            ss.Contains("FETCH", StringComparison.OrdinalIgnoreCase)
                || ss.Contains("TOP", StringComparison.OrdinalIgnoreCase)
        );
    }

    [Fact]
    public void HavingClause_AllDialects_GeneratesCorrectSQL()
    {
        var lql = """
            orders
            |> group_by(orders.user_id)
            |> having(fn(group) => count(*) > 5)
            |> select(orders.user_id, count(*) as order_count)
            """;
        var (pg, ss, sl) = ConvertToAllDialects(lql);

        foreach (var sql in new[] { pg, ss, sl })
        {
            Assert.Contains("HAVING", sql, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("GROUP BY", sql, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void LeftJoin_AllDialects_GeneratesLeftJoinSQL()
    {
        var lql = """
            users
            |> left_join(orders, on = users.id = orders.user_id)
            |> select(users.name, orders.total)
            """;
        var (pg, ss, sl) = ConvertToAllDialects(lql);

        foreach (var sql in new[] { pg, ss, sl })
        {
            Assert.Contains("LEFT", sql, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("JOIN", sql, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("orders", sql, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("user_id", sql, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("total", sql, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void MultipleJoins_AllDialects_GeneratesCorrectSQL()
    {
        var lql = """
            users
            |> join(orders, on = users.id = orders.user_id)
            |> join(products, on = orders.product_id = products.id)
            |> select(users.name, orders.total, products.name)
            """;
        var (pg, ss, sl) = ConvertToAllDialects(lql);

        foreach (var sql in new[] { pg, ss, sl })
        {
            Assert.Contains("users", sql, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("orders", sql, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("products", sql, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("user_id", sql, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("product_id", sql, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("JOIN", sql, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(2, sql.Split("JOIN", StringSplitOptions.None).Length - 1);
        }
    }
}
