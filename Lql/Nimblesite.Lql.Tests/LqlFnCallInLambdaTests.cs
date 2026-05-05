using Nimblesite.Lql.Postgres;
using Nimblesite.Sql.Model;
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
    private static string ToPg(string lql)
    {
        var stmt = LqlStatementConverter.ToStatement(lql);
        Assert.True(
            stmt is Outcome.Result<LqlStatement, SqlError>.Ok<LqlStatement, SqlError>,
            stmt is Outcome.Result<LqlStatement, SqlError>.Error<LqlStatement, SqlError> e
                ? e.Value.Message
                : "expected Ok"
        );
        var ok = (Outcome.Result<LqlStatement, SqlError>.Ok<LqlStatement, SqlError>)stmt;
        var result = ok.Value.ToPostgreSql();
        Assert.True(
            result is Outcome.Result<string, SqlError>.Ok<string, SqlError>,
            result is Outcome.Result<string, SqlError>.Error<string, SqlError> e2
                ? e2.Value.Message
                : "expected transpile Ok"
        );
        return ((Outcome.Result<string, SqlError>.Ok<string, SqlError>)result).Value;
    }

    [Fact]
    public void Lambda_BareFnCall_NoArgs_PassesThrough()
    {
        var sql = ToPg("t |> filter(fn(x) => some_fn())");
        Assert.Contains("some_fn()", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void Lambda_BareFnCall_StringArgs_PassesThrough()
    {
        var sql = ToPg("t |> filter(fn(x) => is_member('a', 'b'))");
        Assert.Contains("is_member('a', 'b')", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void Lambda_BareFnCall_QualifiedIdentArg_StripsLambdaPrefix()
    {
        // x is the lambda var -> x.tenant_id should emit as 'tenant_id'.
        var sql = ToPg("t |> filter(fn(x) => is_member('u', x.tenant_id))");
        Assert.Contains("is_member('u', tenant_id)", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("x.tenant_id", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void Lambda_BareFnCall_NestedFnCallArg_PassesThrough()
    {
        var sql = ToPg("t |> filter(fn(x) => is_member(app_user_id(), app_tenant_id()))");
        // Outer fn is emitted via ProcessFnCallExprToSql (lowercase preserved).
        // Nested fn args go through ExtractFunctionCall which uppercases the
        // function name -- Postgres treats unquoted names as case-insensitive
        // so APP_USER_ID() and app_user_id() resolve to the same function.
        Assert.Contains("is_member(", sql, StringComparison.Ordinal);
        Assert.Contains("APP_USER_ID()", sql, StringComparison.Ordinal);
        Assert.Contains("APP_TENANT_ID()", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void Lambda_AndCombinationWithBareFnCall_ParsesAndEmits()
    {
        // The right-hand side of AND is a bare fn call -- must not raise
        // 'Unsupported expr type in comparison'.
        var sql = ToPg(
            "t |> filter(fn(x) => x.id = '00000000-0000-0000-0000-000000000000' and is_member('u', x.tenant_id))"
        );
        Assert.Contains("AND", sql, StringComparison.Ordinal);
        Assert.Contains("is_member(", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void Lambda_OrCombinationWithBareFnCall_ParsesAndEmits()
    {
        var sql = ToPg(
            "t |> filter(fn(x) => x.id = '00000000-0000-0000-0000-000000000000' or is_member('u', x.tenant_id))"
        );
        Assert.Contains("OR", sql, StringComparison.Ordinal);
        Assert.Contains("is_member(", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void Lambda_BareFnCall_IntArg_PassesThrough()
    {
        var sql = ToPg("t |> filter(fn(x) => has_role(42))");
        Assert.Contains("has_role(42)", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void Lambda_BareFnCall_DecimalArg_PassesThrough()
    {
        var sql = ToPg("t |> filter(fn(x) => has_balance(1.5))");
        Assert.Contains("has_balance(1.5)", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void Lambda_BareFnCall_IdentArg_PassesThrough()
    {
        var sql = ToPg("t |> filter(fn(x) => some_fn(other_col))");
        Assert.Contains("some_fn(", sql, StringComparison.Ordinal);
        Assert.Contains("other_col", sql, StringComparison.Ordinal);
    }
}
