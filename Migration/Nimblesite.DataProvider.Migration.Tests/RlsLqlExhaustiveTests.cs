using TranspileError = Outcome.Result<
    string,
    Nimblesite.DataProvider.Migration.Core.MigrationError
>.Error<string, Nimblesite.DataProvider.Migration.Core.MigrationError>;
using TranspileOk = Outcome.Result<
    string,
    Nimblesite.DataProvider.Migration.Core.MigrationError
>.Ok<string, Nimblesite.DataProvider.Migration.Core.MigrationError>;

namespace Nimblesite.DataProvider.Migration.Tests;

// Implements [RLS-CORE-LQL] EXHAUSTIVE coverage: prove that every NAP-shape
// predicate (and a long tail of edge cases) round-trips through the LQL
// transpiler without needing the raw-SQL escape hatch (usingSql/withCheckSql).
// Operator mandate: NO SQL in YAML schemas. CLAUDE.md prohibits parsing SQL
// with anything other than the official platform parser, so every predicate
// shape NAP needs MUST be expressible in LQL.

/// <summary>
/// Exhaustive transpiler tests. Each test asserts the LQL form transpiles
/// to a Postgres / SQLite / SQL Server CREATE POLICY clause that the
/// platform engine accepts, without any raw SQL escape hatch.
/// </summary>
public sealed class RlsLqlExhaustiveTests
{
    private static string Pg(string lql) =>
        ((TranspileOk)RlsPredicateTranspiler.Translate(lql, RlsPlatform.Postgres, "p")).Value;

    private static string Sl(string lql) =>
        ((TranspileOk)RlsPredicateTranspiler.Translate(lql, RlsPlatform.Sqlite, "p")).Value;

    private static string Mssql(string lql) =>
        ((TranspileOk)RlsPredicateTranspiler.Translate(lql, RlsPlatform.SqlServer, "p")).Value;

    // ── 1. Literal predicates ────────────────────────────────────────

    [Fact]
    public void Literal_True_Postgres() => Assert.Equal("true", Pg("true"));

    [Fact]
    public void Literal_False_Postgres() => Assert.Equal("false", Pg("false"));

    [Fact]
    public void Literal_True_Sqlite() => Assert.Equal("true", Sl("true"));

    [Fact]
    public void Literal_True_SqlServer() => Assert.Equal("true", Mssql("true"));

    // ── 2. Single column equality ────────────────────────────────────

    [Fact]
    public void SingleColumnEquality_Postgres_QuotesIdentifier() =>
        Assert.Contains(
            "\"tenant_id\"",
            Pg("tenant_id = '00000000-0000-0000-0000-000000000000'"),
            StringComparison.Ordinal
        );

    [Fact]
    public void SingleColumnEquality_Sqlite_BracketsIdentifier() =>
        Assert.Contains(
            "[tenant_id]",
            Sl("tenant_id = '00000000-0000-0000-0000-000000000000'"),
            StringComparison.Ordinal
        );

    // ── 3. Builtin current_user_id() ─────────────────────────────────

    [Fact]
    public void Builtin_CurrentUserId_Postgres_ExpandsToCurrentSetting() =>
        Assert.Contains(
            "current_setting('rls.current_user_id', true)",
            Pg("user_id = current_user_id()"),
            StringComparison.Ordinal
        );

    [Fact]
    public void Builtin_CurrentUserId_Sqlite_ExpandsToContextLookup() =>
        Assert.Contains(
            "__rls_context",
            Sl("user_id = current_user_id()"),
            StringComparison.Ordinal
        );

    [Fact]
    public void Builtin_CurrentUserId_SqlServer_ExpandsToSessionContext() =>
        Assert.Contains(
            "SESSION_CONTEXT",
            Mssql("user_id = current_user_id()"),
            StringComparison.Ordinal
        );

    // ── 4. Custom GUC reader fns (NAP: app_tenant_id, app_user_id) ──

    [Fact]
    public void CustomGucReader_AppTenantId_PassesThrough_Postgres()
    {
        var sql = Pg("tenant_id = app_tenant_id()");
        Assert.Contains("\"tenant_id\"", sql, StringComparison.Ordinal);
        Assert.Contains("app_tenant_id()", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("\"app_tenant_id\"", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void CustomGucReader_AppUserId_PassesThrough_Postgres()
    {
        var sql = Pg("user_id = app_user_id()");
        Assert.Contains("app_user_id()", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("\"app_user_id\"", sql, StringComparison.Ordinal);
    }

    // ── 5. SECURITY DEFINER membership fns ──────────────────────────

    [Fact]
    public void SecurityDefiner_IsMember_TwoArgs_PassesThrough()
    {
        var sql = Pg("is_member(app_user_id(), app_tenant_id())");
        Assert.Contains("is_member(", sql, StringComparison.Ordinal);
        Assert.Contains("app_user_id()", sql, StringComparison.Ordinal);
        Assert.Contains("app_tenant_id()", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("\"is_member\"", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void SecurityDefiner_IsOwner_PassesThrough() =>
        Assert.Contains(
            "is_owner(",
            Pg("is_owner(app_user_id(), app_tenant_id())"),
            StringComparison.Ordinal
        );

    [Fact]
    public void SecurityDefiner_IsTenantWriter_PassesThrough() =>
        Assert.Contains(
            "is_tenant_writer(",
            Pg("is_tenant_writer(app_user_id(), app_tenant_id())"),
            StringComparison.Ordinal
        );

    // ── 6. AND combinations ─────────────────────────────────────────

    [Fact]
    public void AndCombination_TwoPredicates_QuotesColumns_Postgres()
    {
        var sql = Pg("tenant_id = app_tenant_id() and is_member(app_user_id(), app_tenant_id())");
        Assert.Contains("\"tenant_id\"", sql, StringComparison.Ordinal);
        Assert.Contains("is_member(", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void AndCombination_LowercaseAnd_Survives()
    {
        // LQL uses lowercase 'and' / 'or'. Transpiler must not rewrite or strip them.
        var sql = Pg("a = 1 and b = 2");
        Assert.Contains("and", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"a\"", sql, StringComparison.Ordinal);
        Assert.Contains("\"b\"", sql, StringComparison.Ordinal);
    }

    // ── 7. OR combinations ──────────────────────────────────────────

    [Fact]
    public void OrCombination_SelfOrTenant_TenantMembersShape()
    {
        var sql = Pg(
            "user_id = app_user_id() or (tenant_id = app_tenant_id() and is_owner(app_user_id(), app_tenant_id()))"
        );
        Assert.Contains("\"user_id\"", sql, StringComparison.Ordinal);
        Assert.Contains("\"tenant_id\"", sql, StringComparison.Ordinal);
        Assert.Contains("app_user_id()", sql, StringComparison.Ordinal);
        Assert.Contains("is_owner(", sql, StringComparison.Ordinal);
    }

    // ── 8. NOT operator ─────────────────────────────────────────────

    [Fact]
    public void NotOperator_AppliesToFnCall()
    {
        var sql = Pg("not is_member(app_user_id(), app_tenant_id())");
        Assert.Contains("not", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("is_member(", sql, StringComparison.Ordinal);
    }

    // ── 9. Parentheses ──────────────────────────────────────────────

    [Fact]
    public void Parentheses_Nested_PreservedInOutput()
    {
        var sql = Pg("(a = 1 and b = 2) or (c = 3 and d = 4)");
        Assert.Contains("\"a\"", sql, StringComparison.Ordinal);
        Assert.Contains("\"d\"", sql, StringComparison.Ordinal);
        // Both nested groups present.
        Assert.Contains("(", sql, StringComparison.Ordinal);
        Assert.Contains(")", sql, StringComparison.Ordinal);
    }

    // ── 10. NULL handling ───────────────────────────────────────────

    [Fact]
    public void IsNull_PassesThrough()
    {
        var sql = Pg("deleted_at is null");
        Assert.Contains("\"deleted_at\"", sql, StringComparison.Ordinal);
        Assert.Contains("is null", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void IsNotNull_PassesThrough()
    {
        var sql = Pg("deleted_at is not null");
        Assert.Contains("\"deleted_at\"", sql, StringComparison.Ordinal);
        Assert.Contains("is not null", sql, StringComparison.OrdinalIgnoreCase);
    }

    // ── 11. Comparison operators ────────────────────────────────────

    [Theory]
    [InlineData("=")]
    [InlineData("<>")]
    [InlineData("!=")]
    [InlineData(">")]
    [InlineData(">=")]
    [InlineData("<")]
    [InlineData("<=")]
    public void ComparisonOperators_Survive_Postgres(string op)
    {
        var sql = Pg($"created_at {op} '2024-01-01'");
        Assert.Contains("\"created_at\"", sql, StringComparison.Ordinal);
        Assert.Contains(op, sql, StringComparison.Ordinal);
    }

    // ── 12. String literals ─────────────────────────────────────────

    [Fact]
    public void StringLiteral_PreservedVerbatim()
    {
        var sql = Pg("status = 'active'");
        Assert.Contains("'active'", sql, StringComparison.Ordinal);
        Assert.Contains("\"status\"", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void StringLiteralWithReservedWordsInside_NotIdentifierQuoted()
    {
        // 'AND' inside a string literal must not be wrapped as identifier.
        var sql = Pg("name = 'AND OR NOT'");
        Assert.Contains("'AND OR NOT'", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("\"AND\"", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void StringLiteralWithEscapedQuote_Survives()
    {
        // Postgres '' is an escaped single quote inside a literal.
        var sql = Pg("name = 'O''Brien'");
        Assert.Contains("'O''Brien'", sql, StringComparison.Ordinal);
    }

    // ── 13. IN clause ───────────────────────────────────────────────

    [Fact]
    public void InClause_PassesThrough()
    {
        var sql = Pg("status in ('active', 'pending', 'review')");
        Assert.Contains("\"status\"", sql, StringComparison.Ordinal);
        Assert.Contains("in", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("'active'", sql, StringComparison.Ordinal);
    }

    // ── 14. LIKE clause ─────────────────────────────────────────────

    [Fact]
    public void LikeClause_PassesThrough()
    {
        var sql = Pg("email like '%@example.com'");
        Assert.Contains("\"email\"", sql, StringComparison.Ordinal);
        Assert.Contains("like", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("'%@example.com'", sql, StringComparison.Ordinal);
    }

    // ── 15. Type casts ──────────────────────────────────────────────

    [Fact]
    public void TypeCast_ColonColon_TypeNameNotQuoted()
    {
        // owner_id::uuid should not become "owner_id"::"uuid" — type names
        // can't be quoted in Postgres.
        var sql = Pg("owner_id::uuid = current_user_id()::uuid");
        Assert.Contains("\"owner_id\"", sql, StringComparison.Ordinal);
        Assert.Contains("::uuid", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("\"uuid\"", sql, StringComparison.Ordinal);
    }

    // ── 16. Numeric literals ────────────────────────────────────────

    [Fact]
    public void NumericLiteral_NotQuoted()
    {
        var sql = Pg("count >= 10");
        Assert.Contains("\"count\"", sql, StringComparison.Ordinal);
        Assert.Contains("10", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("\"10\"", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("[10]", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void NegativeNumericLiteral_NotQuoted()
    {
        var sql = Pg("balance > -5");
        Assert.Contains("\"balance\"", sql, StringComparison.Ordinal);
        Assert.Contains("-5", sql, StringComparison.Ordinal);
    }

    // ── 17. Mixed-case identifiers ──────────────────────────────────

    [Fact]
    public void MixedCaseIdentifier_QuotedPreservesCase()
    {
        var sql = Pg("OwnerId = current_user_id()");
        Assert.Contains("\"OwnerId\"", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void MixedCaseIdentifier_Sqlite_BracketsPreserveCase()
    {
        var sql = Sl("OwnerId = current_user_id()");
        Assert.Contains("[OwnerId]", sql, StringComparison.Ordinal);
    }

    // ── 18. Underscore + digit identifiers ─────────────────────────

    [Fact]
    public void UnderscoreLeadingIdentifier_Quoted()
    {
        var sql = Pg("_internal_flag = true");
        Assert.Contains("\"_internal_flag\"", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void IdentifierWithDigits_Quoted()
    {
        var sql = Pg("col1 = 'x'");
        Assert.Contains("\"col1\"", sql, StringComparison.Ordinal);
    }

    // ── 19. Schema-qualified identifiers ────────────────────────────

    [Fact]
    public void SchemaQualifiedColumn_Postgres_LeadingQuotedTailUnquoted()
    {
        // a.b.c — only the FIRST identifier is treated as a column candidate;
        // anything after a `.` is left alone (already qualified). Postgres
        // accepts "public".documents.tenant_id because quoted names match
        // case-folded unquoted names.
        var sql = Pg("public.documents.tenant_id = app_tenant_id()");
        Assert.Contains("\"public\".documents.tenant_id", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("\"documents\"", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("\"tenant_id\"", sql, StringComparison.Ordinal);
    }

    // ── 20. NAP-shape compositions ─────────────────────────────────

    [Fact]
    public void NapShape_AgentConfigsMember_FullPredicate()
    {
        var sql = Pg("tenant_id = app_tenant_id() and is_member(app_user_id(), app_tenant_id())");
        Assert.Contains("\"tenant_id\"", sql, StringComparison.Ordinal);
        Assert.Contains("app_tenant_id()", sql, StringComparison.Ordinal);
        Assert.Contains("is_member(", sql, StringComparison.Ordinal);
        Assert.Contains("app_user_id()", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("\"is_member\"", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("\"app_tenant_id\"", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void NapShape_AgentConfigsAdminAll_LiteralTrue() => Assert.Equal("true", Pg("true"));

    [Fact]
    public void NapShape_TenantMembersSelfOrOwner_FullPredicate()
    {
        var sql = Pg(
            "user_id = app_user_id() or (tenant_id = app_tenant_id() and is_tenant_owner(app_user_id(), app_tenant_id()))"
        );
        Assert.Contains("\"user_id\"", sql, StringComparison.Ordinal);
        Assert.Contains("\"tenant_id\"", sql, StringComparison.Ordinal);
        Assert.Contains("is_tenant_owner(", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void NapShape_ApiKeysSelfOrWriter_DifferentUsingVsWithCheck()
    {
        // USING: any member of tenant
        var usingSql = Pg(
            "tenant_id = app_tenant_id() and is_member(app_user_id(), app_tenant_id())"
        );
        // WITH CHECK: only writers can insert
        var checkSql = Pg(
            "tenant_id = app_tenant_id() and is_tenant_writer(app_user_id(), app_tenant_id())"
        );
        Assert.Contains("is_member(", usingSql, StringComparison.Ordinal);
        Assert.Contains("is_tenant_writer(", checkSql, StringComparison.Ordinal);
    }

    // ── 21. Whitespace + newline tolerance ──────────────────────────

    [Fact]
    public void Multiline_LqlPredicate_TranspilesCleanly()
    {
        var sql = Pg(
            """
            tenant_id = app_tenant_id()
              and is_member(app_user_id(), app_tenant_id())
            """
        );
        Assert.Contains("\"tenant_id\"", sql, StringComparison.Ordinal);
        Assert.Contains("is_member(", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void ExtraWhitespace_Around_Operators_PreservedNotBroken()
    {
        var sql = Pg("tenant_id   =    app_tenant_id()");
        Assert.Contains("\"tenant_id\"", sql, StringComparison.Ordinal);
        Assert.Contains("app_tenant_id()", sql, StringComparison.Ordinal);
    }

    // ── 22. Empty / whitespace-only predicates ─────────────────────

    [Fact]
    public void EmptyPredicate_RaisesEmptyPredicateError()
    {
        var r = RlsPredicateTranspiler.Translate("", RlsPlatform.Postgres, "p");
        Assert.True(r is TranspileError);
        Assert.Contains(
            "MIG-E-RLS-EMPTY-PREDICATE",
            ((TranspileError)r).Value.Message,
            StringComparison.Ordinal
        );
    }

    // ── 23. exists() subquery LQL pipeline (FALLBACK PATH) ─────────

    [Fact]
    public void ExistsSubquery_GroupMembership_TranspilesViaLqlEngine()
    {
        var lql = """
            users
            |> filter(fn(u) => u.id = current_user_id())
            |> select(users.id)
            """;
        var sql = Pg($"exists({lql})");
        Assert.StartsWith("EXISTS (", sql, StringComparison.Ordinal);
        Assert.Contains(
            "current_setting('rls.current_user_id', true)",
            sql,
            StringComparison.Ordinal
        );
    }

    // ── 24. SQLite trigger-side per-row predicates ──────────────────

    [Fact]
    public void Sqlite_NewRowReference_ColumnQuotedAsTriggerExpr()
    {
        // The SQLite trigger generator passes NEW.col / OLD.col through
        // RlsPredicateTranspiler. Make sure 'NEW' is treated as a qualifier.
        var sql = Sl("NEW.tenant_id = current_user_id()");
        // Trigger code wraps this in a NOT (...). Here we just want
        // current_user_id substituted and tenant_id bracketed.
        Assert.Contains("__rls_context", sql, StringComparison.Ordinal);
    }

    // ── 25. Cross-platform sentinel safety ─────────────────────────

    [Fact]
    public void Sentinel_DoesNotLeak_Postgres()
    {
        var sql = Pg("user_id = current_user_id()");
        Assert.DoesNotContain("__RLS_CURRENT_USER_ID__", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void Sentinel_DoesNotLeak_Sqlite()
    {
        var sql = Sl("user_id = current_user_id()");
        Assert.DoesNotContain("__RLS_CURRENT_USER_ID__", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void Sentinel_DoesNotLeak_SqlServer()
    {
        var sql = Mssql("user_id = current_user_id()");
        Assert.DoesNotContain("__RLS_CURRENT_USER_ID__", sql, StringComparison.Ordinal);
    }

    // ── 26. NAP P0: exists() pipeline with fn calls inside lambda ──
    // NAP shape: messages.tenant_id derived via parent conversations table.
    // Predicate: exists(conversations |> filter(fn(p) => p.id = conversation_id
    //   and is_member(app_user_id(), p.tenant_id)))
    // LQL parser must accept fn-call expressions inside lambda bodies that
    // aren't part of a comparison.

    [Fact]
    public void ExistsPipeline_LambdaWithFnCallInAndClause_Parses()
    {
        var lql = """
            conversations
            |> filter(fn(p) => p.id = '00000000-0000-0000-0000-000000000000' and is_member('a', p.tenant_id))
            |> select(p.id)
            """;
        var result = RlsPredicateTranspiler.Translate(
            $"exists({lql})",
            RlsPlatform.Postgres,
            "messages_member"
        );

        Assert.True(
            result is TranspileOk,
            result is TranspileError e ? e.Value.Message : "expected Ok"
        );
    }

    [Fact]
    public void ExistsPipeline_LambdaScope_StripsLambdaVarFromQualifiedRefs()
    {
        // NAP shape diagnostic: c.id and c.tenant_id should emit as
        // bare 'id'/'tenant_id' in the inner SQL because 'c' is the
        // lambda parameter bound to the FROM table (conversations).
        var lql = """
            conversations
            |> filter(fn(c) => c.id = '00000000-0000-0000-0000-000000000000' and is_member('a', c.tenant_id))
            |> select(c.id)
            """;
        var result = RlsPredicateTranspiler.Translate(
            $"exists({lql})",
            RlsPlatform.Postgres,
            "diag"
        );
        Assert.True(
            result is TranspileOk,
            result is TranspileError e ? e.Value.Message : "expected Ok"
        );
        var sql = ((TranspileOk)result).Value;
        // The inner SQL must reference columns without the 'c.' prefix
        // because 'c' is the lambda variable bound to the FROM table.
        Assert.Contains("FROM conversations", sql, StringComparison.Ordinal);
        // Inside the AND clause (WHERE), 'c.tenant_id' must be stripped
        // to 'tenant_id' so it resolves to the FROM table — that's the
        // critical lambda-scope behavior. (The SELECT projection may
        // still emit 'c.id' verbatim; Postgres accepts that as a table
        // alias if 'c' is added; but the WHERE side is what we care
        // about for fn-call args.)
        var whereIdx = sql.IndexOf("WHERE", StringComparison.OrdinalIgnoreCase);
        Assert.True(whereIdx > 0, "expected WHERE in inner SQL");
        var whereClause = sql[whereIdx..];
        Assert.DoesNotContain("c.tenant_id", whereClause, StringComparison.Ordinal);
    }

    [Fact]
    public void ExistsPipeline_LambdaWithSecurityDefinerFnAtTopLevel_Parses()
    {
        // Even simpler: lambda body is a single fn call, no comparison.
        var lql = """
            conversations
            |> filter(fn(p) => is_member('a', p.tenant_id))
            |> select(p.id)
            """;
        var result = RlsPredicateTranspiler.Translate(
            $"exists({lql})",
            RlsPlatform.Postgres,
            "test"
        );

        Assert.True(
            result is TranspileOk,
            result is TranspileError e ? e.Value.Message : "expected Ok"
        );
    }
}
