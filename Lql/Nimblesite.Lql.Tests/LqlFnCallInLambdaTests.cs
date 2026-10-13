using Nimblesite.Lql.Postgres;
using Nimblesite.Lql.SQLite;
using Nimblesite.Lql.SqlServer;
using Nimblesite.Sql.Model;
using Outcome;
using static global::Tests.Shared.ResultAssertions;
using static global::Tests.Shared.StringAssertions;

namespace Nimblesite.Lql.Tests;

// Coverage for ProcessFnCallExprToSql + ProcessFnCallArgToSql added in
// LqlToAstVisitor for GitHub issues #40/#41 (NAP RLS bare fn calls in
// lambda bodies, e.g. exists(parent |> filter(fn(p) => p.id = id and
// is_member(app_user_id(), p.tenant_id)))).

/// <summary>
/// Targeted unit tests for the bare-function-call branch of LQL's lambda
/// body and its argument-shape handling. These shapes are not exercised by
/// the file-based fixture tests but are required for RLS predicates that
/// call SECURITY DEFINER functions.
/// </summary>
public sealed partial class LqlFnCallInLambdaTests
{
    private static string[] ToAllDialects(string lql)
    {
        var parsed = global::Tests.Shared.ResultAssertions.ExactOk(
            actual: LqlStatementConverter.ToStatement(lql)
        );
        return
        [
            Sql(parsed.Value.ToPostgreSql()),
            Sql(parsed.Value.ToSqlServer()),
            Sql(parsed.Value.ToSQLite()),
        ];
    }

    private static string Sql(Result<string, SqlError> result) =>
        global::Tests.Shared.ResultAssertions.ExactOk(actual: result).Value;

    [Fact]
    public void Lambda_BareFnCall_NoArgs_PassesThrough()
    {
        foreach (var sql in ToAllDialects("t |> filter(fn(x) => some_fn())"))
        {
            Assert.Contains("some_fn()", sql, StringComparison.Ordinal);
            ContainsAll(
                actual: sql,
                expected: ["WHERE", "FROM"],
                comparison: StringComparison.OrdinalIgnoreCase
            );
        }
    }

    [Theory]
    // Lambda_BareFnCall_StringArgs_PassesThrough
    [MemberData(nameof(Lambda_BareFnCall_StringArgs_PassesThrough_CasesInlineCases))]
    // Lambda_BareFnCall_IntArg_PassesThrough

    // Lambda_BareFnCall_DecimalArg_PassesThrough

    public void Lambda_BareFnCall_StringArgs_PassesThrough_Cases(
        string caseValue0,
        string caseValue1,
        string caseValue3
    )
    {
        foreach (var sql in ToAllDialects(caseValue0))
        {
            Assert.Contains(caseValue1, sql, StringComparison.Ordinal);
            Assert.Contains("WHERE", sql, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(caseValue3, sql, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Lambda_BareFnCall_QualifiedIdentArg_StripsLambdaPrefix()
    {
        // x is the lambda var -> x.tenant_id should emit as 'tenant_id'.
        foreach (var sql in ToAllDialects("t |> filter(fn(x) => is_member('u', x.tenant_id))"))
        {
            Assert.Contains("is_member('u', tenant_id)", sql, StringComparison.Ordinal);
            Assert.DoesNotContain("x.tenant_id", sql, StringComparison.Ordinal);
            Assert.Contains("WHERE", sql, StringComparison.OrdinalIgnoreCase);
        }
    }

    // Implements [CI-TEST-SHARED-CASE-MATRIX].
    [Theory]
    [MemberData(nameof(IndependentCommentScenarioData1))]
    public void Lambda_BareFnCall_NestedFnCallArg_PassesThrough_CommentScenarioCases(
        string scenarioName,
        System.String scenario0,
        System.String scenario1,
        System.String scenario2,
        System.String scenario3
    )
    {
        NamedCase(caseName: scenarioName);
        foreach (var sql in ToAllDialects(scenario0))
        {
            // Outer fn is emitted via ProcessFnCallExprToSql (lowercase preserved).
            // Nested fn args go through ExtractFunctionCall which uppercases the
            // function name -- Postgres treats unquoted names as case-insensitive
            // so APP_USER_ID() and app_user_id() resolve to the same function.
            ContainsAll(
                actual: sql,
                expected: [scenario1, scenario2, scenario3],
                comparison: StringComparison.Ordinal
            );
            Assert.Contains("WHERE", sql, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void Lambda_BareFnCall_IdentArg_PassesThrough()
    {
        foreach (var sql in ToAllDialects("t |> filter(fn(x) => some_fn(other_col))"))
        {
            global::Nimblesite.Lql.Tests.LqlDialectE2ETests.VerifyLifetimePhase1(
                input0: sql,
                value0: "some_fn(",
                value1: "other_col",
                value3: "WHERE"
            );
            Assert.DoesNotContain("some_fn()", sql, StringComparison.Ordinal);
        }
    }

    // Implements [LQL-PREDICATE-IN-LIST].
    [Theory]
    [MemberData(nameof(InList_ChangingMembership_PreservesExactPredicateAcrossDialectsInlineCases))]
    public void InList_ChangingMembership_PreservesExactPredicateAcrossDialects(
        string initialMembers,
        string updatedMembers
    )
    {
        var initial = ToAllDialects($"members |> filter(fn(m) => m.role in ({initialMembers}))");
        var updated = ToAllDialects($"members |> filter(fn(m) => m.role in ({updatedMembers}))");

        for (var dialect = 0; dialect < initial.Length; dialect++)
        {
            Assert.Contains(
                $"role IN ({initialMembers})",
                initial[dialect],
                StringComparison.OrdinalIgnoreCase
            );
            Assert.Contains(
                $"role IN ({updatedMembers})",
                updated[dialect],
                StringComparison.OrdinalIgnoreCase
            );
            Assert.Contains("WHERE", initial[dialect], StringComparison.OrdinalIgnoreCase);
            Assert.Contains("WHERE", updated[dialect], StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(" OR ", initial[dialect], StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(" OR ", updated[dialect], StringComparison.OrdinalIgnoreCase);
            Assert.NotEqual(initial[dialect], updated[dialect]);
        }
    }
}
