using Nimblesite.Lql.Postgres;
using Nimblesite.Lql.SQLite;
using Nimblesite.Lql.SqlServer;
using Nimblesite.Sql.Model;
using Outcome;
using Xunit;
using static global::Tests.Shared.StringAssertions;

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
        var lql = Data_CountFunction_AllDialects_GeneratesCorrectSQL_1;
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
        var lql = Data_SumAndAvgFunctions_AllDialects_GeneratesCorrectSQL_1;
        var (pg, ss, sl) = ConvertToAllDialects(lql);

        foreach (var sql in new[] { pg, ss, sl })
        {
            AssertBothSqlFragments(input0: sql, value0: "SUM", value1: "AVG");
            ContainsAll(
                actual: sql,
                expected: ["total_amount", "avg_amount"],
                comparison: StringComparison.Ordinal
            );
            Assert.Contains("GROUP BY", sql, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void CountDistinctFunction_AllDialects_GeneratesCorrectSQL()
    {
        var lql = Data_CountDistinctFunction_AllDialects_GeneratesCorrectSQL_1;
        var (pg, ss, sl) = ConvertToAllDialects(lql);

        foreach (var sql in new[] { pg, ss, sl })
        {
            AssertBothSqlFragments(input0: sql, value0: "COUNT", value1: "DISTINCT");
            AssertBothSqlFragments(input0: sql, value0: "COUNT(DISTINCT", value1: "product_id");
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
            AssertBothSqlFragments(input0: sql, value0: "UPPER", value1: "LOWER");
            ContainsAll(
                actual: sql,
                expected: ["upper_name", "lower_email"],
                comparison: StringComparison.Ordinal
            );
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
            AssertBothSqlFragments(input0: sql, value0: "SELECT DISTINCT", value1: "country");
        }
    }

    [Fact]
    public void OffsetAndLimit_AllDialects_GeneratesCorrectPagination()
    {
        var lql = Data_OffsetAndLimit_AllDialects_GeneratesCorrectPagination_1;
        var (pg, ss, sl) = ConvertToAllDialects(lql);

        AssertBothSqlFragments(input0: pg, value0: "OFFSET", value1: "LIMIT");
        ContainsAll(actual: pg, expected: ["20", "10"], comparison: StringComparison.Ordinal);
        AssertBothSqlFragments(input0: sl, value0: "OFFSET", value1: "LIMIT");
        ContainsAll(actual: sl, expected: ["20", "10"], comparison: StringComparison.Ordinal);
        // SQL Server uses OFFSET...FETCH or TOP
        Assert.Contains("OFFSET", ss, StringComparison.OrdinalIgnoreCase);
        ContainsAll(actual: ss, expected: ["20", "10"], comparison: StringComparison.Ordinal);
        Assert.True(
            ss.Contains("FETCH", StringComparison.OrdinalIgnoreCase)
                || ss.Contains("TOP", StringComparison.OrdinalIgnoreCase)
        );
    }

    [Fact]
    public void HavingClause_AllDialects_GeneratesCorrectSQL()
    {
        var lql = Data_HavingClause_AllDialects_GeneratesCorrectSQL_1;
        var (pg, ss, sl) = ConvertToAllDialects(lql);

        foreach (var sql in new[] { pg, ss, sl })
        {
            AssertBothSqlFragments(input0: sql, value0: "HAVING", value1: "GROUP BY");
        }
    }

    [Fact]
    public void LeftJoin_AllDialects_GeneratesLeftJoinSQL()
    {
        var lql = Data_LeftJoin_AllDialects_GeneratesLeftJoinSQL_1;
        var (pg, ss, sl) = ConvertToAllDialects(lql);

        foreach (var sql in new[] { pg, ss, sl })
        {
            AssertBothSqlFragments(input0: sql, value0: "LEFT", value1: "JOIN");
            AssertBothSqlFragments(input0: sql, value0: "orders", value1: "user_id");
            Assert.Contains("total", sql, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void MultipleJoins_AllDialects_GeneratesCorrectSQL()
    {
        var lql = Data_MultipleJoins_AllDialects_GeneratesCorrectSQL_1;
        var (pg, ss, sl) = ConvertToAllDialects(lql);

        foreach (var sql in new[] { pg, ss, sl })
        {
            AssertBothSqlFragments(input0: sql, value0: "users", value1: "orders");
            AssertBothSqlFragments(input0: sql, value0: "products", value1: "user_id");
            AssertBothSqlFragments(input0: sql, value0: "product_id", value1: "JOIN");
            Assert.Equal(2, sql.Split("JOIN", StringSplitOptions.None).Length - 1);
        }
    }
}
