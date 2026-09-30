using Nimblesite.Lql.Postgres;
using Nimblesite.Lql.SQLite;
using Nimblesite.Lql.SqlServer;
using Nimblesite.Sql.Model;
using Outcome;
using Xunit;

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
public sealed class LqlFnCallInLambdaTests
{
    private static string[] ToAllDialects(string lql)
    {
        var parsed = Assert.IsType<Result<LqlStatement, SqlError>.Ok<LqlStatement, SqlError>>(
            LqlStatementConverter.ToStatement(lql)
        );
        return
        [
            Sql(parsed.Value.ToPostgreSql()),
            Sql(parsed.Value.ToSqlServer()),
            Sql(parsed.Value.ToSQLite()),
        ];
    }

    private static string Sql(Result<string, SqlError> result) =>
        Assert.IsType<Result<string, SqlError>.Ok<string, SqlError>>(result).Value;

    [Fact]
    public void Lambda_BareFnCall_NoArgs_PassesThrough()
    {
        foreach (var sql in ToAllDialects("t |> filter(fn(x) => some_fn())"))
        {
            Assert.Contains("some_fn()", sql, StringComparison.Ordinal);
            Assert.Contains("WHERE", sql, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("FROM", sql, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void Lambda_BareFnCall_StringArgs_PassesThrough()
    {
        foreach (var sql in ToAllDialects("t |> filter(fn(x) => is_member('a', 'b'))"))
        {
            Assert.Contains("is_member('a', 'b')", sql, StringComparison.Ordinal);
            Assert.Contains("WHERE", sql, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("is_member()", sql, StringComparison.Ordinal);
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

    [Fact]
    public void Lambda_BareFnCall_NestedFnCallArg_PassesThrough()
    {
        foreach (
            var sql in ToAllDialects(
                "t |> filter(fn(x) => is_member(app_user_id(), app_tenant_id()))"
            )
        )
        {
            // Outer fn is emitted via ProcessFnCallExprToSql (lowercase preserved).
            // Nested fn args go through ExtractFunctionCall which uppercases the
            // function name -- Postgres treats unquoted names as case-insensitive
            // so APP_USER_ID() and app_user_id() resolve to the same function.
            Assert.Contains("is_member(", sql, StringComparison.Ordinal);
            Assert.Contains("APP_USER_ID()", sql, StringComparison.Ordinal);
            Assert.Contains("APP_TENANT_ID()", sql, StringComparison.Ordinal);
            Assert.Contains("WHERE", sql, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void Lambda_AndCombinationWithBareFnCall_ParsesAndEmits()
    {
        // The right-hand side of AND is a bare fn call -- must not raise
        // 'Unsupported expr type in comparison'.
        foreach (
            var sql in ToAllDialects(
                "t |> filter(fn(x) => x.id = '00000000-0000-0000-0000-000000000000' and is_member('u', x.tenant_id))"
            )
        )
        {
            Assert.Contains("AND", sql, StringComparison.Ordinal);
            Assert.Contains("is_member(", sql, StringComparison.Ordinal);
            Assert.Contains("00000000-0000-0000-0000-000000000000", sql, StringComparison.Ordinal);
            Assert.Contains("WHERE", sql, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void Lambda_OrCombinationWithBareFnCall_ParsesAndEmits()
    {
        foreach (
            var sql in ToAllDialects(
                "t |> filter(fn(x) => x.id = '00000000-0000-0000-0000-000000000000' or is_member('u', x.tenant_id))"
            )
        )
        {
            Assert.Contains("OR", sql, StringComparison.Ordinal);
            Assert.Contains("is_member(", sql, StringComparison.Ordinal);
            Assert.Contains("00000000-0000-0000-0000-000000000000", sql, StringComparison.Ordinal);
            Assert.Contains("WHERE", sql, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void Lambda_BareFnCall_IntArg_PassesThrough()
    {
        foreach (var sql in ToAllDialects("t |> filter(fn(x) => has_role(42))"))
        {
            Assert.Contains("has_role(42)", sql, StringComparison.Ordinal);
            Assert.Contains("WHERE", sql, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("has_role()", sql, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Lambda_BareFnCall_DecimalArg_PassesThrough()
    {
        foreach (var sql in ToAllDialects("t |> filter(fn(x) => has_balance(1.5))"))
        {
            Assert.Contains("has_balance(1.5)", sql, StringComparison.Ordinal);
            Assert.Contains("WHERE", sql, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("has_balance()", sql, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Lambda_BareFnCall_IdentArg_PassesThrough()
    {
        foreach (var sql in ToAllDialects("t |> filter(fn(x) => some_fn(other_col))"))
        {
            Assert.Contains("some_fn(", sql, StringComparison.Ordinal);
            Assert.Contains("other_col", sql, StringComparison.Ordinal);
            Assert.Contains("WHERE", sql, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("some_fn()", sql, StringComparison.Ordinal);
        }
    }

    // Implements [LQL-PREDICATE-IN-LIST].
    [Theory]
    [InlineData("'owner', 'admin'", "'owner', 'admin', 'auditor'")]
    [InlineData("1, 2", "1, 2, 3")]
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
