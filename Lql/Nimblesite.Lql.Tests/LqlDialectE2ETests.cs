using Nimblesite.Lql.Postgres;
using Nimblesite.Lql.SQLite;
using Nimblesite.Lql.SqlServer;
using Nimblesite.Sql.Model;
using Outcome;
using static global::Tests.Shared.ResultAssertions;
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
            Assert.Fail($"Parse failed: {(Error(actual: result)).Value.DetailedMessage}");
            return default;
        }

        var stmt = parseOk.Value;

        var pg = stmt.ToPostgreSql();
        var ss = stmt.ToSqlServer();
        var sl = stmt.ToSQLite();

        // Implements [LQL-TEST-SHARED-DIALECT-RESULTS]. Preserve conversion and check order.
        return (
            Ok(actual: pg, message: "PostgreSql conversion failed").Value,
            Ok(actual: ss, message: "SqlServer conversion failed").Value,
            Ok(actual: sl, message: "SQLite conversion failed").Value
        );
    }

    [Fact]
    public void CountFunction_AllDialects_GeneratesCorrectSQL()
    {
        var (pg, ss, sl) = VerifySharedChecks1(
            input0: Data_CountFunction_AllDialects_GeneratesCorrectSQL_1
        );

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
        var (pg, ss, sl) = VerifySharedChecks1(
            input0: Data_SumAndAvgFunctions_AllDialects_GeneratesCorrectSQL_1
        );

        foreach (var sql in new[] { pg, ss, sl })
        {
            AssertBothSqlFragments(input0: sql, value0: "SUM", value1: "AVG");
            VerifyLifetimePhase1(
                input0: sql,
                value0: "total_amount",
                value1: "avg_amount",
                value3: "GROUP BY"
            );
        }
    }

    [Fact]
    public void CountDistinctFunction_AllDialects_GeneratesCorrectSQL()
    {
        var (pg, ss, sl) = VerifySharedChecks1(
            input0: Data_CountDistinctFunction_AllDialects_GeneratesCorrectSQL_1
        );

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
        var (pg, ss, sl) = VerifyReusableWorkflow2(
            input0: ConvertToAllDialects,
            value0: """
            users
            |> select(upper(users.name) as upper_name, lower(users.email) as lower_email)
            """
        );

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
    public void OffsetAndLimit_AllDialects_GeneratesCorrectPagination()
    {
        var (pg, ss, sl) = VerifySharedChecks1(
            input0: Data_OffsetAndLimit_AllDialects_GeneratesCorrectPagination_1
        );

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
        var (pg, ss, sl) = VerifySharedChecks1(
            input0: Data_HavingClause_AllDialects_GeneratesCorrectSQL_1
        );

        foreach (var sql in new[] { pg, ss, sl })
        {
            AssertBothSqlFragments(input0: sql, value0: "HAVING", value1: "GROUP BY");
        }
    }

    [Fact]
    public void LeftJoin_AllDialects_GeneratesLeftJoinSQL()
    {
        var (pg, ss, sl) = VerifySharedChecks1(
            input0: Data_LeftJoin_AllDialects_GeneratesLeftJoinSQL_1
        );

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
        var (pg, ss, sl) = VerifySharedChecks1(
            input0: Data_MultipleJoins_AllDialects_GeneratesCorrectSQL_1
        );

        foreach (var sql in new[] { pg, ss, sl })
        {
            AssertBothSqlFragments(input0: sql, value0: "users", value1: "orders");
            AssertBothSqlFragments(input0: sql, value0: "products", value1: "user_id");
            AssertBothSqlFragments(input0: sql, value0: "product_id", value1: "JOIN");
            Assert.Equal(2, sql.Split("JOIN", StringSplitOptions.None).Length - 1);
        }
    }
}
