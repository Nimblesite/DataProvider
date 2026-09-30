using Xunit;

namespace Nimblesite.Lql.Tests;

public sealed partial class LqlDialectE2ETests
{
    // Implements [LQL-PIPELINE-COMPOSITION].
    [Fact]
    public void ProgressivePipeline_KeepsEarlierOperationsOnEveryDialect()
    {
        var browse = ConvertToAllDialects("users |> select(users.id, users.name)");
        var filtered = ConvertToAllDialects(
            "users |> join(orders, on = users.id = orders.user_id) |> filter(fn(row) => row.orders.total > 100) |> select(users.id, orders.total)"
        );
        var ranked = ConvertToAllDialects(
            "users |> join(orders, on = users.id = orders.user_id) |> filter(fn(row) => row.orders.total > 100) |> order_by(orders.total desc) |> limit(50) |> select(users.id, orders.total)"
        );

        AssertProgressivePipeline(browse.PostgreSql, filtered.PostgreSql, ranked.PostgreSql);
        AssertProgressivePipeline(browse.SQLite, filtered.SQLite, ranked.SQLite);
        AssertProgressivePipeline(browse.SqlServer, filtered.SqlServer, ranked.SqlServer);
    }

    private static void AssertProgressivePipeline(string browse, string filtered, string ranked)
    {
        Assert.Contains("FROM users", browse, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("JOIN", browse, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("WHERE", browse, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("FROM users", filtered, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("JOIN orders", filtered, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("WHERE", filtered, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("100", filtered, StringComparison.Ordinal);
        Assert.Contains("JOIN orders", ranked, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("WHERE", ranked, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ORDER BY", ranked, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("DESC", ranked, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("50", ranked, StringComparison.Ordinal);
        Assert.NotEqual(browse, filtered);
        Assert.NotEqual(filtered, ranked);
    }

    [Fact]
    public void ComplexFilterWithAndOr_AllDialects_GeneratesCorrectSQL()
    {
        var lql = """
            users
            |> filter(fn(row) => row.users.age > 18 and row.users.country = 'US' or row.users.status = 'premium')
            |> select(users.id, users.name)
            """;
        var (pg, ss, sl) = ConvertToAllDialects(lql);

        foreach (var sql in new[] { pg, ss, sl })
        {
            Assert.Contains("WHERE", sql, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("AND", sql, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("OR", sql, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void OrderByDescending_AllDialects_GeneratesDescSQL()
    {
        var lql = """
            users
            |> order_by(users.age desc, users.name asc)
            |> select(users.id, users.name, users.age)
            """;
        var (pg, ss, sl) = ConvertToAllDialects(lql);

        foreach (var sql in new[] { pg, ss, sl })
        {
            Assert.Contains("ORDER BY", sql, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("DESC", sql, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("ASC", sql, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void FilterWithStringLiteral_AllDialects_PreservesQuotes()
    {
        var lql = """
            users
            |> filter(fn(row) => row.users.status = 'active')
            |> select(users.id, users.name)
            """;
        var (pg, ss, sl) = ConvertToAllDialects(lql);

        foreach (var sql in new[] { pg, ss, sl })
        {
            Assert.Contains("'active'", sql, StringComparison.Ordinal);
            Assert.Contains("WHERE", sql, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void FilterWithNumericComparison_AllDialects_GeneratesCorrectSQL()
    {
        var lql = """
            products
            |> filter(fn(row) => row.products.price >= 100 and row.products.price <= 500)
            |> select(products.id, products.name, products.price)
            """;
        var (pg, ss, sl) = ConvertToAllDialects(lql);

        foreach (var sql in new[] { pg, ss, sl })
        {
            Assert.Contains(">=", sql, StringComparison.Ordinal);
            Assert.Contains("<=", sql, StringComparison.Ordinal);
            Assert.Contains("WHERE", sql, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void JoinWithFilterAndOrderBy_AllDialects_FullPipelineWorks()
    {
        var lql = """
            users
            |> join(orders, on = users.id = orders.user_id)
            |> filter(fn(row) => row.orders.total > 100)
            |> order_by(orders.total desc)
            |> limit(50)
            |> select(users.name, orders.total, orders.status)
            """;
        var (pg, ss, sl) = ConvertToAllDialects(lql);

        foreach (var sql in new[] { pg, ss, sl })
        {
            Assert.Contains("JOIN", sql, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("WHERE", sql, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("ORDER BY", sql, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("DESC", sql, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void GroupByWithMultipleAggregates_AllDialects_GeneratesCorrectSQL()
    {
        var lql = """
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
            """;
        var (pg, ss, sl) = ConvertToAllDialects(lql);

        foreach (var sql in new[] { pg, ss, sl })
        {
            Assert.Contains("COUNT", sql, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("SUM", sql, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("AVG", sql, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("GROUP BY", sql, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("ORDER BY", sql, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void SimpleTableReference_AllDialects_GeneratesSelectStar()
    {
        var lql = "users |> select(users.id, users.name)";
        var (pg, ss, sl) = ConvertToAllDialects(lql);

        foreach (var sql in new[] { pg, ss, sl })
        {
            Assert.Contains("SELECT", sql, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("FROM", sql, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void ArithmeticExpressions_AllDialects_GeneratesCorrectSQL()
    {
        var lql = """
            products
            |> select(
                products.id,
                products.price * products.quantity as total_value,
                products.price + 10 as price_plus_ten
            )
            """;
        var (pg, ss, sl) = ConvertToAllDialects(lql);

        foreach (var sql in new[] { pg, ss, sl })
        {
            Assert.Contains("*", sql, StringComparison.Ordinal);
            Assert.Contains("+", sql, StringComparison.Ordinal);
            Assert.Contains("total_value", sql, StringComparison.Ordinal);
            Assert.Contains("price_plus_ten", sql, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void CaseExpression_AllDialects_GeneratesCaseWhenSQL()
    {
        var lql = """
            orders
            |> select(
                orders.id,
                case
                    when orders.total > 1000 then orders.total * 0.95
                    when orders.total > 500 then orders.total * 0.97
                    else orders.total
                end as discounted
            )
            """;
        var (pg, ss, sl) = ConvertToAllDialects(lql);

        foreach (var sql in new[] { pg, ss, sl })
        {
            Assert.Contains("CASE", sql, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("WHEN", sql, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("THEN", sql, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("ELSE", sql, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("END", sql, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void FilterWithNotEquals_AllDialects_GeneratesCorrectOperator()
    {
        var lql = """
            users
            |> filter(fn(row) => row.users.status != 'deleted')
            |> select(users.id, users.name)
            """;
        var (pg, ss, sl) = ConvertToAllDialects(lql);

        foreach (var sql in new[] { pg, ss, sl })
        {
            Assert.Contains("WHERE", sql, StringComparison.OrdinalIgnoreCase);
            Assert.True(
                sql.Contains("!=", StringComparison.Ordinal)
                    || sql.Contains("<>", StringComparison.Ordinal),
                "Should contain != or <> operator"
            );
        }
    }

    [Fact]
    public void SelectWithColumnAlias_AllDialects_GeneratesAliasedColumns()
    {
        var lql = """
            users
            |> select(users.id, users.name as full_name, users.email as contact_email)
            """;
        var (pg, ss, sl) = ConvertToAllDialects(lql);

        foreach (var sql in new[] { pg, ss, sl })
        {
            Assert.Contains("full_name", sql, StringComparison.Ordinal);
            Assert.Contains("contact_email", sql, StringComparison.Ordinal);
            Assert.Contains("AS", sql, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void LimitOnly_AllDialects_GeneratesLimitSQL()
    {
        var lql = """
            users |> limit(25) |> select(users.id, users.name)
            """;
        var (pg, ss, sl) = ConvertToAllDialects(lql);

        Assert.Contains("LIMIT", pg, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("LIMIT", sl, StringComparison.OrdinalIgnoreCase);
        // SQL Server uses TOP
        Assert.True(
            ss.Contains("TOP", StringComparison.OrdinalIgnoreCase)
                || ss.Contains("FETCH", StringComparison.OrdinalIgnoreCase),
            "SQL Server should use TOP or FETCH for limit"
        );
    }
}
