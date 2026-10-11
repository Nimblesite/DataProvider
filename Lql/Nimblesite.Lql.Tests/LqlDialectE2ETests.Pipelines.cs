using Xunit;
using static global::Tests.Shared.StringAssertions;

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
        ContainsAll(
            actual: filtered,
            expected: ["FROM users", "JOIN orders", "WHERE"],
            comparison: StringComparison.OrdinalIgnoreCase
        );
        Assert.Contains("100", filtered, StringComparison.Ordinal);
        ContainsAll(
            actual: ranked,
            expected: ["JOIN orders", "WHERE", "ORDER BY", "DESC"],
            comparison: StringComparison.OrdinalIgnoreCase
        );
        Assert.Contains("50", ranked, StringComparison.Ordinal);
        Assert.NotEqual(browse, filtered);
        Assert.NotEqual(filtered, ranked);
    }

    [Fact]
    public void ComplexFilterWithAndOr_AllDialects_GeneratesCorrectSQL()
    {
        var lql = Data_ComplexFilterWithAndOr_AllDialects_GeneratesCorrectSQL_1;
        var (pg, ss, sl) = ConvertToAllDialects(lql);

        foreach (var sql in new[] { pg, ss, sl })
        {
            AssertBothSqlFragments(input0: sql, value0: "WHERE", value1: "AND");
            Assert.Contains("OR", sql, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void OrderByDescending_AllDialects_GeneratesDescSQL()
    {
        var lql = Data_OrderByDescending_AllDialects_GeneratesDescSQL_1;
        var (pg, ss, sl) = ConvertToAllDialects(lql);

        foreach (var sql in new[] { pg, ss, sl })
        {
            AssertBothSqlFragments(input0: sql, value0: "ORDER BY", value1: "DESC");
            Assert.Contains("ASC", sql, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void FilterWithStringLiteral_AllDialects_PreservesQuotes()
    {
        var lql = Data_FilterWithStringLiteral_AllDialects_PreservesQuotes_1;
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
        var lql = Data_FilterWithNumericComparison_AllDialects_GeneratesCorrectSQL_1;
        var (pg, ss, sl) = ConvertToAllDialects(lql);

        foreach (var sql in new[] { pg, ss, sl })
        {
            ContainsAll(actual: sql, expected: [">=", "<="], comparison: StringComparison.Ordinal);
            Assert.Contains("WHERE", sql, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void JoinWithFilterAndOrderBy_AllDialects_FullPipelineWorks()
    {
        var lql = Data_JoinWithFilterAndOrderBy_AllDialects_FullPipelineWorks_1;
        var (pg, ss, sl) = ConvertToAllDialects(lql);

        foreach (var sql in new[] { pg, ss, sl })
        {
            AssertBothSqlFragments(input0: sql, value0: "JOIN", value1: "WHERE");
            AssertBothSqlFragments(input0: sql, value0: "ORDER BY", value1: "DESC");
        }
    }

    [Theory]
    // GroupByWithMultipleAggregates_AllDialects_GeneratesCorrectSQL
    [MemberData(
        nameof(GroupByWithMultipleAggregates_AllDialects_GeneratesCorrectSQL_CasesInlineCases)
    )]
    // CaseExpression_AllDialects_GeneratesCaseWhenSQL

    public void GroupByWithMultipleAggregates_AllDialects_GeneratesCorrectSQL_Cases(
        string caseValue0,
        string caseValue1,
        string caseValue2,
        string caseValue3,
        string caseValue4,
        string caseValue5
    )
    {
        var lql = caseValue0;
        var (pg, ss, sl) = ConvertToAllDialects(lql);

        foreach (var sql in new[] { pg, ss, sl })
        {
            ContainsAll(
                actual: sql,
                expected: [caseValue1, caseValue2, caseValue3, caseValue4, caseValue5],
                comparison: StringComparison.OrdinalIgnoreCase
            );
        }
    }

    [Fact]
    public void SimpleTableReference_AllDialects_GeneratesSelectStar()
    {
        var lql = "users |> select(users.id, users.name)";
        var (pg, ss, sl) = ConvertToAllDialects(lql);

        foreach (var sql in new[] { pg, ss, sl })
        {
            AssertBothSqlFragments(input0: sql, value0: "SELECT", value1: "FROM");
        }
    }

    [Fact]
    public void ArithmeticExpressions_AllDialects_GeneratesCorrectSQL()
    {
        var lql = Data_ArithmeticExpressions_AllDialects_GeneratesCorrectSQL_1;
        var (pg, ss, sl) = ConvertToAllDialects(lql);

        foreach (var sql in new[] { pg, ss, sl })
        {
            ContainsAll(
                actual: sql,
                expected: ["*", "+", "total_value", "price_plus_ten"],
                comparison: StringComparison.Ordinal
            );
        }
    }

    [Fact]
    public void FilterWithNotEquals_AllDialects_GeneratesCorrectOperator()
    {
        var lql = Data_FilterWithNotEquals_AllDialects_GeneratesCorrectOperator_1;
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
            ContainsAll(
                actual: sql,
                expected: ["full_name", "contact_email"],
                comparison: StringComparison.Ordinal
            );
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

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private static void AssertBothSqlFragments(
        global::System.String input0,
        string value0,
        string value1
    )
    {
        ContainsAll(
            actual: input0,
            expected: [value0, value1],
            comparison: StringComparison.OrdinalIgnoreCase
        );
    }
}
