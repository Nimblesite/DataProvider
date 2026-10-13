using static global::Tests.Shared.ResultAssertions;
using static global::Tests.Shared.StringAssertions;

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
public sealed partial class RlsLqlExhaustiveTests
{
    private static string Translate(string lql, RlsPlatform platform) =>
        (Ok(actual: RlsPredicateTranspiler.Translate(lql, platform, "p"))).Value;

    private static string Pg(string lql) => Translate(lql, RlsPlatform.Postgres);

    private static string Sl(string lql) => Translate(lql, RlsPlatform.Sqlite);

    private static string Mssql(string lql) => Translate(lql, RlsPlatform.SqlServer);

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

    // Implements [CI-TEST-SHARED-CASE-MATRIX].
    [Theory]
    [MemberData(nameof(CustomGucReader_AppTenantId_PassesThrough_Postgres_ContextCasesData))]
    public void CustomGucReader_AppTenantId_PassesThrough_Postgres_ContextCases(
        string caseName,
        string value0,
        string value1,
        string value2,
        string value3
    )
    {
        Assert.False(condition: string.IsNullOrEmpty(value: caseName));
        ExerciseProjectionCaseMatrix4(
            input0: Pg,
            value0: value0,
            value1: value1,
            value2: value2,
            value3: value3
        );
    }

    // Implements [CI-TEST-SHARED-CASE-MATRIX].
    [Theory]
    [MemberData(nameof(CustomGucReader_AppUserId_PassesThrough_Postgres_ContextCasesData))]
    public void CustomGucReader_AppUserId_PassesThrough_Postgres_ContextCases(
        string caseName,
        string value0,
        string value1,
        string value3
    )
    {
        Assert.False(condition: string.IsNullOrEmpty(value: caseName));
        ExerciseProjectionCaseMatrix5(
            input0: VerifyReusableWorkflow5,
            input1: Pg,
            value0: value0,
            value1: value1,
            value3: value3
        );
    }

    // ── 5. SECURITY DEFINER membership fns ──────────────────────────

    [Fact]
    public void SecurityDefiner_IsMember_TwoArgs_PassesThrough()
    {
        var sql = Pg("is_member(app_user_id(), app_tenant_id())");
        ContainsAll(
            actual: sql,
            expected: ["is_member(", "app_user_id()", "app_tenant_id()"],
            comparison: StringComparison.Ordinal
        );
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

    [Theory]
    // AndCombination_TwoPredicates_QuotesColumns_Postgres
    [MemberData(nameof(AndCombination_TwoPredicates_QuotesColumns_Postgres_CasesInlineCases))]
    // StringLiteral_PreservedVerbatim

    // NegativeNumericLiteral_NotQuoted

    // Multiline_LqlPredicate_TranspilesCleanly

    // ExtraWhitespace_Around_Operators_PreservedNotBroken

    public void AndCombination_TwoPredicates_QuotesColumns_Postgres_Cases(
        string caseValue0,
        string caseValue1,
        string caseValue2
    )
    {
        var sql = Pg(caseValue0);
        ContainsAll(
            actual: sql,
            expected: [caseValue1, caseValue2],
            comparison: StringComparison.Ordinal
        );
    }

    [Fact]
    public void AndCombination_LowercaseAnd_Survives()
    {
        // LQL uses lowercase 'and' / 'or'. Transpiler must not rewrite or strip them.
        var sql = VerifyReusableWorkflow5(
            input0: Pg,
            value0: "a = 1 and b = 2",
            value1: "and",
            value2: StringComparison.OrdinalIgnoreCase
        );
        ContainsAll(
            actual: sql,
            expected: ["\"a\"", "\"b\""],
            comparison: StringComparison.Ordinal
        );
    }

    // ── 7. OR combinations ──────────────────────────────────────────

    // Implements [CI-TEST-SHARED-CASE-MATRIX].
    [Theory]
    [MemberData(nameof(IndependentScenarioData17))]
    public void OrCombination_SelfOrTenant_TenantMembersShape_ScenarioCases(
        string scenarioName,
        System.String scenario0,
        System.String scenario1,
        System.String scenario2,
        System.String scenario3,
        System.String scenario4
    )
    {
        Assert.False(condition: string.IsNullOrEmpty(value: scenarioName));

        var sql = Pg(scenario0);
        ContainsAll(
            actual: sql,
            expected: [scenario1, scenario2, scenario3, scenario4],
            comparison: StringComparison.Ordinal
        );
    }

    // ── 8. NOT operator ─────────────────────────────────────────────

    // Implements [CI-TEST-SHARED-CASE-MATRIX].
    [Theory]
    [MemberData(nameof(NotOperator_AppliesToFnCall_ContextCasesData))]
    public void NotOperator_AppliesToFnCall_ContextCases(
        string caseName,
        string value0,
        string value1,
        global::System.StringComparison value2,
        string value3,
        global::System.StringComparison value4
    )
    {
        Assert.False(condition: string.IsNullOrEmpty(value: caseName));
        ExerciseProjectionCaseMatrix1(
            input0: VerifyReusableWorkflow5,
            input1: Pg,
            value0: value0,
            value1: value1,
            value2: value2,
            value3: value3,
            value4: value4
        );
    }

    // ── 9. Parentheses ──────────────────────────────────────────────

    // ── 10. NULL handling ───────────────────────────────────────────

    // ── 11. Comparison operators ────────────────────────────────────

    [Theory]
    [MemberData(nameof(ComparisonOperators_Survive_PostgresInlineCases))]
    public void ComparisonOperators_Survive_Postgres(string op)
    {
        var sql = Pg($"created_at {op} '2024-01-01'");
        ContainsAll(
            actual: sql,
            expected: ["\"created_at\"", op],
            comparison: StringComparison.Ordinal
        );
    }

    // ── 12. String literals ─────────────────────────────────────────

    // Implements [CI-TEST-SHARED-CASE-MATRIX].
    [Theory]
    [MemberData(nameof(StringLiteralWithEscapedQuote_Survives_ContextCasesData))]
    public void StringLiteralWithEscapedQuote_Survives_ContextCases(
        string caseName,
        Func<string, string> input0,
        string value0,
        string value1
    )
    {
        Assert.False(condition: string.IsNullOrEmpty(value: caseName));
        ExerciseWorkflowCaseMatrix1(input0: input0, value0: value0, value1: value1);
    }

    // ── 13. IN clause ───────────────────────────────────────────────

    // Implements [CI-TEST-SHARED-CASE-MATRIX].
    [Theory]
    [MemberData(nameof(InClause_PassesThrough_ContextCasesData))]
    public void InClause_PassesThrough_ContextCases(
        string caseName,
        string value0,
        string value1,
        string value3,
        string value5
    )
    {
        Assert.False(condition: string.IsNullOrEmpty(value: caseName));
        ExerciseProjectionCaseMatrix2(
            input0: VerifyReusableWorkflow5,
            input1: Pg,
            value0: value0,
            value1: value1,
            value3: value3,
            value5: value5
        );
    }

    // ── 14. LIKE clause ─────────────────────────────────────────────

    // ── 15. Type casts ──────────────────────────────────────────────

    // ── 16. Numeric literals ────────────────────────────────────────

    [Fact]
    public void NumericLiteral_NotQuoted()
    {
        var sql = Pg("count >= 10");
        global::Nimblesite.DataProvider.Migration.Tests.RlsCurrentSettingLqlTests.VerifyCrossClassChecks2(
            input0: sql,
            value0: "\"count\"",
            value1: "10",
            value3: "\"10\""
        );
        Assert.DoesNotContain("[10]", sql, StringComparison.Ordinal);
    }

    // ── 17. Mixed-case identifiers ──────────────────────────────────

    [Theory]
    // MixedCaseIdentifier_QuotedPreservesCase
    [MemberData(nameof(MixedCaseIdentifier_QuotedPreservesCase_CasesInlineCases))]
    // UnderscoreLeadingIdentifier_Quoted

    // IdentifierWithDigits_Quoted

    public void MixedCaseIdentifier_QuotedPreservesCase_Cases(string caseValue0, string caseValue1)
    {
        var sql = Pg(caseValue0);
        Assert.Contains(caseValue1, sql, StringComparison.Ordinal);
    }

    // ── 18. Underscore + digit identifiers ─────────────────────────

    // ── 19. Schema-qualified identifiers ────────────────────────────

    [Fact]
    public void SchemaQualifiedColumn_Postgres_LeadingQuotedTailUnquoted()
    {
        // a.b.c — only the FIRST identifier is treated as a column candidate;
        // anything after a `.` is left alone (already qualified). Postgres
        // accepts "public".documents.tenant_id because quoted names match
        // case-folded unquoted names.
        var sql = VerifyReusableWorkflow5(
            input0: Pg,
            value0: "public.documents.tenant_id = app_tenant_id()",
            value1: "\"public\".documents.tenant_id",
            value2: StringComparison.Ordinal
        );
        Assert.DoesNotContain("\"documents\"", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("\"tenant_id\"", sql, StringComparison.Ordinal);
    }

    // ── 20. NAP-shape compositions ─────────────────────────────────

    [Fact]
    public void NapShape_AgentConfigsMember_FullPredicate()
    {
        var sql = Pg("tenant_id = app_tenant_id() and is_member(app_user_id(), app_tenant_id())");
        VerifyCompletionWorkflow5(
            input0: ContainsAll,
            input1: sql,
            value4: "\"is_member\"",
            value5: "\"app_tenant_id\""
        );
    }

    [Fact]
    public void NapShape_AgentConfigsAdminAll_LiteralTrue() => Assert.Equal("true", Pg("true"));

    [Fact]
    public void NapShape_TenantMembersSelfOrOwner_FullPredicate()
    {
        var sql = Pg(
            "user_id = app_user_id() or (tenant_id = app_tenant_id() and is_tenant_owner(app_user_id(), app_tenant_id()))"
        );
        ContainsAll(
            actual: sql,
            expected: ["\"user_id\"", "\"tenant_id\"", "is_tenant_owner("],
            comparison: StringComparison.Ordinal
        );
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

    // ── 22. Empty / whitespace-only predicates ─────────────────────

    [Fact]
    public void EmptyPredicate_RaisesEmptyPredicateError()
    {
        VerifyCompletionWorkflow3(value0: "", value1: RlsPlatform.Postgres, value2: "p");
    }

    // ── 23. exists() subquery LQL pipeline (FALLBACK PATH) ─────────

    [Fact]
    public void ExistsSubquery_GroupMembership_TranspilesViaLqlEngine()
    {
        var lql = Data_ExistsSubquery_GroupMembership_TranspilesViaLqlEngine_1;
        var sql = Pg($"exists({lql})");
        Assert.StartsWith("EXISTS (", sql, StringComparison.Ordinal);
        Assert.Contains(
            "current_setting('rls.current_user_id', true)",
            sql,
            StringComparison.Ordinal
        );
    }

    // ── 24. SQLite trigger-side per-row predicates ──────────────────

    // ── 25. Cross-platform sentinel safety ─────────────────────────

    // Implements [CI-TEST-SHARED-CASE-MATRIX].
    [Theory]
    [MemberData(nameof(Sentinel_DoesNotLeak_Postgres_ContextCasesData))]
    public void Sentinel_DoesNotLeak_Postgres_ContextCases(
        string caseName,
        Func<string, string> input0
    )
    {
        Assert.False(condition: string.IsNullOrEmpty(value: caseName));
        ExerciseProjectionCaseMatrix7(input0: input0);
    }

    // ── 26. NAP P0: exists() pipeline with fn calls inside lambda ──
    // NAP shape: messages.tenant_id derived via parent conversations table.
    // Predicate: exists(conversations |> filter(fn(p) => p.id = conversation_id
    //   and is_member(app_user_id(), p.tenant_id)))
    // LQL parser must accept fn-call expressions inside lambda bodies that
    // aren't part of a comparison.

    // Implements [CI-TEST-SHARED-CASE-MATRIX].
    [Theory]
    [MemberData(nameof(IndependentCommentScenarioData6))]
    public void ExistsPipeline_LambdaWithFnCallInAndClause_Parses_CommentScenarioCases(
        string scenarioName,
        System.String scenario0,
        System.String scenario1
    )
    {
        Assert.False(string.IsNullOrEmpty(scenarioName));
        var lql = scenario0;
        var result = RlsPredicateTranspiler.Translate(
            $"exists({lql})",
            RlsPlatform.Postgres,
            scenario1
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
        var lql = Data_ExistsPipeline_LambdaScope_StripsLambdaVarFromQualifiedRefs_1;
        var result = RlsPredicateTranspiler.Translate(
            $"exists({lql})",
            RlsPlatform.Postgres,
            "diag"
        );

        var sql = Ok(
            actual: result,
            message: result is TranspileError e ? e.Value.Message : "expected Ok"
        ).Value;
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

    // Implements [CI-TEST-SHARED-SCENARIOS].
    internal static string VerifyReusableWorkflow5(
        Func<string, string> input0,
        string value0,
        string value1,
        global::System.StringComparison value2
    )
    {
        var sql = input0(arg: value0);
        Assert.Contains(value1, sql, value2);

        return sql;
    }

    // Implements [CI-TEST-SHARED-CASE-MATRIX].
    private static void ExerciseWorkflowCaseMatrix1(
        Func<string, string> input0,
        string value0,
        string value1
    )
    { // Postgres '' is an escaped single quote inside a literal.
        var sql = input0(value0);
        Assert.Contains(value1, sql, StringComparison.Ordinal);
    }

    // Implements [CI-TEST-SHARED-CASE-MATRIX].
    private static void ExerciseProjectionCaseMatrix1(
        Func<Func<string, string>, string, string, StringComparison, string> input0,
        Func<string, string> input1,
        string value0,
        string value1,
        global::System.StringComparison value2,
        string value3,
        global::System.StringComparison value4
    )
    {
        var sql = input0(arg1: input1, arg2: value0, arg3: value1, arg4: value2);
        Assert.Contains(value3, sql, value4);
    }

    // Implements [CI-TEST-SHARED-CASE-MATRIX].
    private static void ExerciseProjectionCaseMatrix2(
        Func<Func<string, string>, string, string, StringComparison, string> input0,
        Func<string, string> input1,
        string value0,
        string value1,
        string value3,
        string value5
    )
    {
        var sql = input0(arg1: input1, arg2: value0, arg3: value1, arg4: StringComparison.Ordinal);
        Assert.Contains(value3, sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(value5, sql, StringComparison.Ordinal);
    }

    // Implements [CI-TEST-SHARED-CASE-MATRIX].
    private static void ExerciseProjectionCaseMatrix4(
        Func<string, string> input0,
        string value0,
        string value1,
        string value2,
        string value3
    )
    {
        var sql = input0(arg: value0);
        global::Nimblesite.DataProvider.Migration.Tests.RlsCurrentSettingLqlTests.VerifyCrossClassChecks2(
            input0: sql,
            value0: value1,
            value1: value2,
            value3: value3
        );
    }

    // Implements [CI-TEST-SHARED-CASE-MATRIX].
    private static void ExerciseProjectionCaseMatrix5(
        Func<Func<string, string>, string, string, StringComparison, string> input0,
        Func<string, string> input1,
        string value0,
        string value1,
        string value3
    )
    {
        var sql = input0(arg1: input1, arg2: value0, arg3: value1, arg4: StringComparison.Ordinal);
        Assert.DoesNotContain(value3, sql, StringComparison.Ordinal);
    }

    // Implements [CI-TEST-SHARED-CASE-MATRIX].
    private static void ExerciseProjectionCaseMatrix7(Func<string, string> input0)
    {
        var sql = input0(arg: "user_id = current_user_id()");
        Assert.DoesNotContain("__RLS_CURRENT_USER_ID__", sql, StringComparison.Ordinal);
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    internal static void VerifyCompletionWorkflow3(
        string value0,
        global::Nimblesite.DataProvider.Migration.Core.RlsPlatform value1,
        string value2
    )
    {
        var r =
            global::Nimblesite.DataProvider.Migration.Tests.RlsCurrentSettingLqlTests.VerifyReusableWorkflow11(
                value0: value0,
                value1: value1,
                value2: value2
            );
        Assert.Contains(
            "MIG-E-RLS-EMPTY-PREDICATE",
            (Error(actual: r)).Value.Message,
            StringComparison.Ordinal
        );
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    internal static void VerifyCompletionWorkflow5(
        Action<
            string?,
            System.Collections.Immutable.ImmutableArray<string>,
            StringComparison
        > input0,
        string input1,
        string value4,
        string value5
    )
    {
        global::Nimblesite.DataProvider.Migration.Tests.PostgresRlsDdlTests.VerifyReusableWorkflow17(
            input0: input0,
            input1: input1,
            value0: "\"tenant_id\"",
            value1: "app_tenant_id()",
            value2: "is_member(",
            value3: "app_user_id()",
            value5: value4
        );
        Assert.DoesNotContain(value5, input1, StringComparison.Ordinal);
    }
}
