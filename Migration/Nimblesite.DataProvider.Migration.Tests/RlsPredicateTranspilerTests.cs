using TranspileError = Outcome.Result<
    string,
    Nimblesite.DataProvider.Migration.Core.MigrationError
>.Error<string, Nimblesite.DataProvider.Migration.Core.MigrationError>;
using TranspileOk = Outcome.Result<
    string,
    Nimblesite.DataProvider.Migration.Core.MigrationError
>.Ok<string, Nimblesite.DataProvider.Migration.Core.MigrationError>;

namespace Nimblesite.DataProvider.Migration.Tests;

// Implements [RLS-CORE-LQL] tests from docs/specs/rls-spec.md.

/// <summary>
/// Unit tests for the RLS LQL predicate transpiler.
/// </summary>
public sealed class RlsPredicateTranspilerTests
{
    [Fact]
    public void CurrentUserIdExpression_Postgres_UsesCurrentSetting()
    {
        var expr = RlsPredicateTranspiler.CurrentUserIdExpression(RlsPlatform.Postgres);
        Assert.Equal("current_setting('rls.current_user_id', true)", expr);
    }

    [Fact]
    public void CurrentUserIdExpression_Sqlite_ReadsRlsContextTable()
    {
        var expr = RlsPredicateTranspiler.CurrentUserIdExpression(RlsPlatform.Sqlite);
        Assert.Equal("(SELECT current_user_id FROM [__rls_context] LIMIT 1)", expr);
    }

    [Fact]
    public void CurrentUserIdExpression_SqlServer_UsesSessionContext()
    {
        var expr = RlsPredicateTranspiler.CurrentUserIdExpression(RlsPlatform.SqlServer);
        Assert.Equal("CAST(SESSION_CONTEXT(N'current_user_id') AS NVARCHAR(450))", expr);
    }

    [Fact]
    public void Translate_SimplePredicate_Postgres_QuotesColumnAndSubstitutesUserId()
    {
        var result = RlsPredicateTranspiler.Translate(
            "OwnerId = current_user_id()",
            RlsPlatform.Postgres,
            "owner_isolation"
        );

        Assert.True(result is TranspileOk);
        var sql = ((TranspileOk)result).Value;
        Assert.Contains("\"OwnerId\"", sql, StringComparison.Ordinal);
        Assert.Contains(
            "current_setting('rls.current_user_id', true)",
            sql,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void Translate_SimplePredicate_Sqlite_BracketsColumn()
    {
        var result = RlsPredicateTranspiler.Translate(
            "OwnerId = current_user_id()",
            RlsPlatform.Sqlite,
            "owner_isolation"
        );

        Assert.True(result is TranspileOk);
        var sql = ((TranspileOk)result).Value;
        Assert.Contains("[OwnerId]", sql, StringComparison.Ordinal);
        Assert.Contains("__rls_context", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void Translate_SimplePredicate_SqlServer_BracketsColumnAndUsesSessionContext()
    {
        var result = RlsPredicateTranspiler.Translate(
            "OwnerId = current_user_id()",
            RlsPlatform.SqlServer,
            "owner_isolation"
        );

        Assert.True(result is TranspileOk);
        var sql = ((TranspileOk)result).Value;
        Assert.Contains("[OwnerId]", sql, StringComparison.Ordinal);
        Assert.Contains("SESSION_CONTEXT", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void Translate_EmptyPredicate_ReturnsRlsEmptyPredicateError()
    {
        var result = RlsPredicateTranspiler.Translate("", RlsPlatform.Postgres, "p");

        Assert.True(result is TranspileError);
        var err = ((TranspileError)result).Value;
        Assert.Contains("MIG-E-RLS-EMPTY-PREDICATE", err.Message, StringComparison.Ordinal);
        Assert.Contains("'p'", err.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Translate_WhitespaceOnlyPredicate_ReturnsRlsEmptyPredicateError()
    {
        var result = RlsPredicateTranspiler.Translate("   \n\t  ", RlsPlatform.Sqlite, "noop");

        Assert.True(result is TranspileError);
        Assert.Contains(
            "MIG-E-RLS-EMPTY-PREDICATE",
            ((TranspileError)result).Value.Message,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void Translate_ExistsSubquery_Postgres_WrapsWithExistsAndQuotesIdentifiers()
    {
        const string lql = """
            users
            |> filter(fn(u) => u.id = current_user_id())
            |> select(users.id)
            """;
        var input = $"exists({lql})";

        var result = RlsPredicateTranspiler.Translate(input, RlsPlatform.Postgres, "ex");

        Assert.True(
            result is TranspileOk,
            result is TranspileError e ? e.Value.Message : "expected Ok"
        );
        var sql = ((TranspileOk)result).Value;
        Assert.StartsWith("EXISTS (", sql, StringComparison.Ordinal);
        Assert.EndsWith(")", sql, StringComparison.Ordinal);
        Assert.Contains(
            "current_setting('rls.current_user_id', true)",
            sql,
            StringComparison.Ordinal
        );
        // Sentinel must not leak into output.
        Assert.DoesNotContain(
            RlsPredicateTranspilerTestAccess.Sentinel,
            sql,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void Translate_ExistsSubquery_Sqlite_SubstitutesContextLookup()
    {
        const string lql = """
            users
            |> filter(fn(u) => u.id = current_user_id())
            |> select(users.id)
            """;
        var input = $"exists({lql})";

        var result = RlsPredicateTranspiler.Translate(input, RlsPlatform.Sqlite, "ex");

        Assert.True(
            result is TranspileOk,
            result is TranspileError e ? e.Value.Message : "expected Ok"
        );
        var sql = ((TranspileOk)result).Value;
        Assert.StartsWith("EXISTS (", sql, StringComparison.Ordinal);
        Assert.Contains("__rls_context", sql, StringComparison.Ordinal);
        Assert.DoesNotContain(
            RlsPredicateTranspilerTestAccess.Sentinel,
            sql,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void Translate_ExistsSubquery_BadLql_ReturnsLqlParseError()
    {
        var result = RlsPredicateTranspiler.Translate(
            "exists(this is not valid lql @@@)",
            RlsPlatform.Postgres,
            "broken"
        );

        Assert.True(result is TranspileError);
        var err = ((TranspileError)result).Value;
        Assert.Contains("MIG-E-RLS-LQL", err.Message, StringComparison.Ordinal);
        Assert.Contains("'broken'", err.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Translate_BooleanLiteralPredicate_RoundTripsKeywordsUnquoted()
    {
        var result = RlsPredicateTranspiler.Translate("true", RlsPlatform.Postgres, "any");
        Assert.True(result is TranspileOk);
        Assert.Equal("true", ((TranspileOk)result).Value);
    }

    [Fact]
    public void Translate_StringLiteralWithReservedWordInside_NotQuoted()
    {
        // Inside string literal, words like AND must not be wrapped as identifiers.
        var result = RlsPredicateTranspiler.Translate(
            "OwnerName = 'AND OR NOT'",
            RlsPlatform.Postgres,
            "p"
        );
        Assert.True(result is TranspileOk);
        var sql = ((TranspileOk)result).Value;
        Assert.Contains("'AND OR NOT'", sql, StringComparison.Ordinal);
        Assert.Contains("\"OwnerName\"", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("\"AND\"", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void Translate_NapShape_LqlWithCustomFnCalls_PassesThroughUnquoted()
    {
        // NAP's exact pattern: tenant_id = app_tenant_id() AND is_member(app_user_id(), app_tenant_id())
        // Required: column refs quoted, fn names + fn calls emitted verbatim, AND/and lowercase OK.
        var result = RlsPredicateTranspiler.Translate(
            "tenant_id = app_tenant_id() and is_member(app_user_id(), app_tenant_id())",
            RlsPlatform.Postgres,
            "tenant_member"
        );

        Assert.True(
            result is TranspileOk,
            result is TranspileError e ? e.Value.Message : "expected Ok"
        );
        var sql = ((TranspileOk)result).Value;

        Assert.Contains("\"tenant_id\"", sql, StringComparison.Ordinal);
        Assert.Contains("app_tenant_id()", sql, StringComparison.Ordinal);
        Assert.Contains("is_member(", sql, StringComparison.Ordinal);
        Assert.Contains("app_user_id()", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("\"app_tenant_id\"", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("\"is_member\"", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("\"app_user_id\"", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void Translate_NapShape_LiteralTrue_PassesThrough()
    {
        // admin_all policies use literal `true` predicate.
        var result = RlsPredicateTranspiler.Translate("true", RlsPlatform.Postgres, "admin_all");
        Assert.True(result is TranspileOk);
        Assert.Equal("true", ((TranspileOk)result).Value);
    }

    [Fact]
    public void Translate_OrCombinationWithFnCalls_PassesThrough()
    {
        // tenant_members_self_or_owner shape:
        // user_id = app_user_id() OR (tenant_id = app_tenant_id() AND is_owner(app_user_id(), app_tenant_id()))
        var result = RlsPredicateTranspiler.Translate(
            "user_id = app_user_id() or (tenant_id = app_tenant_id() and is_owner(app_user_id(), app_tenant_id()))",
            RlsPlatform.Postgres,
            "self_or_owner"
        );

        Assert.True(result is TranspileOk);
        var sql = ((TranspileOk)result).Value;
        Assert.Contains("\"user_id\"", sql, StringComparison.Ordinal);
        Assert.Contains("\"tenant_id\"", sql, StringComparison.Ordinal);
        Assert.Contains("app_user_id()", sql, StringComparison.Ordinal);
        Assert.Contains("app_tenant_id()", sql, StringComparison.Ordinal);
        Assert.Contains("is_owner(", sql, StringComparison.Ordinal);
    }
}

/// <summary>
/// Bridge to the internal sentinel constant for assertions.
/// </summary>
internal static class RlsPredicateTranspilerTestAccess
{
    public const string Sentinel = "__RLS_CURRENT_USER_ID__";
}
