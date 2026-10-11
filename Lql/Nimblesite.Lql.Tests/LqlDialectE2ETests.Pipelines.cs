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

    // Implements [CI-TEST-SHARED-CASE-MATRIX].
    [Theory]
    [MemberData(nameof(IndependentScenarioData10))]
    public void ComplexFilterWithAndOr_AllDialects_GeneratesCorrectSQL_ScenarioCases(
        string scenarioName,
        System.String scenario0,
        System.String scenario1,
        System.String scenario2,
        System.String scenario3
    )
    {
        Assert.False(condition: string.IsNullOrEmpty(value: scenarioName));

        var (pg, ss, sl) = VerifySharedChecks1(input0: scenario0);

        foreach (var sql in new[] { pg, ss, sl })
        {
            AssertBothSqlFragments(input0: sql, value0: scenario1, value1: scenario2);
            Assert.Contains(scenario3, sql, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void FilterWithStringLiteral_AllDialects_PreservesQuotes()
    {
        var (pg, ss, sl) = VerifySharedChecks1(
            input0: Data_FilterWithStringLiteral_AllDialects_PreservesQuotes_1
        );

        foreach (var sql in new[] { pg, ss, sl })
        {
            Assert.Contains("'active'", sql, StringComparison.Ordinal);
            Assert.Contains("WHERE", sql, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void FilterWithNumericComparison_AllDialects_GeneratesCorrectSQL()
    {
        var (pg, ss, sl) = VerifySharedChecks1(
            input0: Data_FilterWithNumericComparison_AllDialects_GeneratesCorrectSQL_1
        );

        foreach (var sql in new[] { pg, ss, sl })
        {
            VerifyLifetimePhase1(input0: sql, value0: ">=", value1: "<=", value3: "WHERE");
        }
    }

    [Fact]
    public void JoinWithFilterAndOrderBy_AllDialects_FullPipelineWorks()
    {
        var (pg, ss, sl) = VerifySharedChecks1(
            input0: Data_JoinWithFilterAndOrderBy_AllDialects_FullPipelineWorks_1
        );

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
        var (pg, ss, sl) = VerifySharedChecks1(input0: caseValue0);

        foreach (var sql in new[] { pg, ss, sl })
        {
            ContainsAll(
                actual: sql,
                expected: [caseValue1, caseValue2, caseValue3, caseValue4, caseValue5],
                comparison: StringComparison.OrdinalIgnoreCase
            );
        }
    }

    // Implements [CI-TEST-SHARED-CASE-MATRIX].
    [Theory]
    [MemberData(nameof(SimpleTableReference_AllDialects_GeneratesSelectStar_ContextCasesData))]
    public void SimpleTableReference_AllDialects_GeneratesSelectStar_ContextCases(
        string caseName,
        string value0,
        string value1,
        string value2
    )
    {
        Assert.False(condition: string.IsNullOrEmpty(value: caseName));
        ExerciseProjectionCaseMatrix1(
            input0: VerifyReusableWorkflow2,
            input1: ConvertToAllDialects,
            input2: AssertBothSqlFragments,
            value0: value0,
            value1: value1,
            value2: value2
        );
    }

    [Fact]
    public void ArithmeticExpressions_AllDialects_GeneratesCorrectSQL()
    {
        var (pg, ss, sl) = VerifySharedChecks1(
            input0: Data_ArithmeticExpressions_AllDialects_GeneratesCorrectSQL_1
        );

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
        var (pg, ss, sl) = VerifySharedChecks1(
            input0: Data_FilterWithNotEquals_AllDialects_GeneratesCorrectOperator_1
        );

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
        var (pg, ss, sl) = VerifyReusableWorkflow2(
            input0: ConvertToAllDialects,
            value0: """
            users
            |> select(users.id, users.name as full_name, users.email as contact_email)
            """
        );

        foreach (var sql in new[] { pg, ss, sl })
        {
            VerifyLifetimePhase1(
                input0: sql,
                value0: "full_name",
                value1: "contact_email",
                value3: "AS"
            );
        }
    }

    [Fact]
    public void LimitOnly_AllDialects_GeneratesLimitSQL()
    {
        var (pg, ss, sl) = VerifyReusableWorkflow2(
            input0: ConvertToAllDialects,
            value0: """
            users |> limit(25) |> select(users.id, users.name)
            """
        );

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

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private static (string, string, string) VerifySharedChecks1(string input0)
    {
        var lql = input0;
        var (pg, ss, sl) = ConvertToAllDialects(lql);

        return (pg, ss, sl);
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    internal static void VerifyLifetimePhase1(
        string input0,
        string value0,
        string value1,
        string value3
    )
    {
        ContainsAll(
            actual: input0,
            expected: [value0, value1],
            comparison: StringComparison.Ordinal
        );
        Assert.Contains(value3, input0, StringComparison.OrdinalIgnoreCase);
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    internal static (string, string, string) VerifyReusableWorkflow2(
        Func<string, (string PostgreSql, string SqlServer, string SQLite)> input0,
        string value0
    )
    {
        var lql = value0;
        var (pg, ss, sl) = input0(arg: lql);

        return (pg, ss, sl);
    }

    // Implements [CI-TEST-SHARED-CASE-MATRIX].
    private static void ExerciseProjectionCaseMatrix1(
        Func<
            Func<string, (string PostgreSql, string SqlServer, string SQLite)>,
            string,
            (string, string, string)
        > input0,
        Func<string, (string PostgreSql, string SqlServer, string SQLite)> input1,
        Action<string, string, string> input2,
        string value0,
        string value1,
        string value2
    )
    {
        var (pg, ss, sl) = input0(arg1: input1, arg2: value0);

        foreach (var sql in new[] { pg, ss, sl })
        {
            input2(arg1: sql, arg2: value1, arg3: value2);
        }
    }
}
