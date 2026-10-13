using static global::Tests.Shared.ResultAssertions;
using static global::Tests.Shared.StringAssertions;

namespace Nimblesite.DataProvider.Migration.Tests;

// Implements [RLS-CORE-LQL] tests from docs/specs/rls-spec.md.

/// <summary>
/// Unit tests for the RLS LQL predicate transpiler.
/// </summary>
public sealed partial class RlsPredicateTranspilerTests
{
    // Implements [CI-TEST-SHARED-CASE-MATRIX].
    [Theory]
    [MemberData(nameof(CurrentUserIdExpression_Postgres_UsesCurrentSetting_ContextCasesData))]
    public void CurrentUserIdExpression_Postgres_UsesCurrentSetting_ContextCases(
        string caseName,
        global::Nimblesite.DataProvider.Migration.Core.RlsPlatform value0,
        string value1
    )
    {
        Assert.False(condition: string.IsNullOrEmpty(value: caseName));
        ExerciseProjectionCaseMatrix8(value0: value0, value1: value1);
    }

    // Implements [CI-TEST-SHARED-CASE-MATRIX].
    [Theory]
    [MemberData(
        nameof(Translate_SimplePredicate_Postgres_QuotesColumnAndSubstitutesUserId_CasesData)
    )]
    public void Translate_SimplePredicate_Postgres_QuotesColumnAndSubstitutesUserId_Cases(
        string caseName,
        global::Nimblesite.DataProvider.Migration.Core.RlsPlatform value1,
        global::System.String value3,
        global::System.String value4
    )
    {
        Assert.False(condition: string.IsNullOrEmpty(value: caseName));
        ExerciseEnumCaseMatrix1(value1: value1, value3: value3, value4: value4);
    }

    // Implements [CI-TEST-SHARED-CASE-MATRIX].
    [Theory]
    [MemberData(nameof(IndependentScenarioData18))]
    public void Translate_EmptyPredicate_ReturnsRlsEmptyPredicateError_ScenarioCases(
        string scenarioName,
        System.String scenario0,
        System.String scenario1,
        System.String scenario2,
        System.String scenario3
    )
    {
        Assert.False(condition: string.IsNullOrEmpty(value: scenarioName));

        var result = RlsPredicateTranspiler.Translate(scenario0, RlsPlatform.Postgres, scenario1);

        var err = Expected<TranspileError>(actual: result).Value;
        VerifyCrossClassChecks3(input0: err, value0: scenario2, value2: scenario3);
    }

    [Fact]
    public void Translate_WhitespaceOnlyPredicate_ReturnsRlsEmptyPredicateError()
    {
        global::Nimblesite.DataProvider.Migration.Tests.RlsLqlExhaustiveTests.VerifyCompletionWorkflow3(
            value0: "   \n\t  ",
            value1: RlsPlatform.Sqlite,
            value2: "noop"
        );
    }

    [Fact]
    public void Translate_ExistsSubquery_Postgres_WrapsWithExistsAndQuotesIdentifiers()
    {
        const string lql =
            Data_Translate_ExistsSubquery_Postgres_WrapsWithExistsAndQuotesIdentifiers_1;
        var sql = VerifyReusableWorkflow25(input0: lql, value0: RlsPlatform.Postgres);
        Assert.EndsWith(")", sql, StringComparison.Ordinal);
        VerifyReusableWorkflow31(
            input0: sql,
            value0: "current_setting('rls.current_user_id', true)"
        );
    }

    [Fact]
    public void Translate_ExistsSubquery_Sqlite_SubstitutesContextLookup()
    {
        const string lql = Data_Translate_ExistsSubquery_Sqlite_SubstitutesContextLookup_1;
        var sql = VerifyReusableWorkflow25(input0: lql, value0: RlsPlatform.Sqlite);
        VerifyReusableWorkflow31(input0: sql, value0: "__rls_context");
    }

    // Implements [CI-TEST-SHARED-CASE-MATRIX].
    [Theory]
    [MemberData(
        nameof(Translate_BooleanLiteralPredicate_RoundTripsKeywordsUnquoted_ContextCasesData)
    )]
    public void Translate_BooleanLiteralPredicate_RoundTripsKeywordsUnquoted_ContextCases(
        string caseName,
        string value2
    )
    {
        Assert.False(condition: string.IsNullOrEmpty(value: caseName));
        ExerciseProjectionCaseMatrix9(value2: value2);
    }

    [Fact]
    public void Translate_StringLiteralWithReservedWordInside_NotQuoted()
    {
        // Inside string literal, words like AND must not be wrapped as identifiers.
        // Inside string literal, words like AND must not be wrapped as identifiers.
        // Inside string literal, words like AND must not be wrapped as identifiers.
        var sql = SqlFragments(
            read: () =>
                RlsPredicateTranspiler.Translate(
                    "OwnerName = 'AND OR NOT'",
                    RlsPlatform.Postgres,
                    "p"
                ),
            expected: ["'AND OR NOT'", "\"OwnerName\""],
            comparison: StringComparison.Ordinal
        );
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

        var sql = Ok(
            actual: result,
            message: result is TranspileError e ? e.Value.Message : "expected Ok"
        ).Value;

        global::Nimblesite.DataProvider.Migration.Tests.RlsLqlExhaustiveTests.VerifyCompletionWorkflow5(
            input0: ContainsAll,
            input1: sql,
            value4: "\"app_tenant_id\"",
            value5: "\"is_member\""
        );
        Assert.DoesNotContain("\"app_user_id\"", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void Translate_OrCombinationWithFnCalls_PassesThrough()
    {
        // tenant_members_self_or_owner shape:
        // user_id = app_user_id() OR (tenant_id = app_tenant_id() AND is_owner(app_user_id(), app_tenant_id()))
        // tenant_members_self_or_owner shape:
        // user_id = app_user_id() OR (tenant_id = app_tenant_id() AND is_owner(app_user_id(), app_tenant_id()))
        // tenant_members_self_or_owner shape:
        // user_id = app_user_id() OR (tenant_id = app_tenant_id() AND is_owner(app_user_id(), app_tenant_id()))
        SqlFragments(
            read: () =>
                RlsPredicateTranspiler.Translate(
                    "user_id = app_user_id() or (tenant_id = app_tenant_id() and is_owner(app_user_id(), app_tenant_id()))",
                    RlsPlatform.Postgres,
                    "self_or_owner"
                ),
            expected:
            [
                "\"user_id\"",
                "\"tenant_id\"",
                "app_user_id()",
                "app_tenant_id()",
                "is_owner(",
            ],
            comparison: StringComparison.Ordinal
        );
    }

    // Implements [CI-TEST-SHARED-CASE-MATRIX].
    private static void ExerciseEnumCaseMatrix1(
        global::Nimblesite.DataProvider.Migration.Core.RlsPlatform value1,
        global::System.String value3,
        global::System.String value4
    )
    {
        SqlFragments(
            read: () =>
                RlsPredicateTranspiler.Translate(
                    "OwnerId = current_user_id()",
                    value1,
                    "owner_isolation"
                ),
            expected: [value3, value4],
            comparison: StringComparison.Ordinal
        );
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    internal static void VerifyCrossClassChecks3(
        MigrationError input0,
        string value0,
        string value2
    )
    {
        Assert.Contains(value0, input0.Message, StringComparison.Ordinal);
        Assert.Contains(value2, input0.Message, StringComparison.Ordinal);
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    internal static string VerifyReusableWorkflow25(
        string input0,
        global::Nimblesite.DataProvider.Migration.Core.RlsPlatform value0
    )
    {
        var input = $"exists({input0})";

        var result = RlsPredicateTranspiler.Translate(input, value0, "ex");

        var sql = Ok(
            actual: result,
            message: result is TranspileError e ? e.Value.Message : "expected Ok"
        ).Value;
        Assert.StartsWith("EXISTS (", sql, StringComparison.Ordinal);

        return sql;
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    internal static void VerifyReusableWorkflow31(string input0, string value0)
    {
        Assert.Contains(value0, input0, StringComparison.Ordinal);
        // Sentinel must not leak into output.
        Assert.DoesNotContain(
            RlsPredicateTranspilerTestAccess.Sentinel,
            input0,
            StringComparison.Ordinal
        );
    }

    // Implements [CI-TEST-SHARED-CASE-MATRIX].
    private static void ExerciseProjectionCaseMatrix8(
        global::Nimblesite.DataProvider.Migration.Core.RlsPlatform value0,
        string value1
    )
    {
        var expr = RlsPredicateTranspiler.CurrentUserIdExpression(value0);
        Assert.Equal(value1, expr);
    }

    // Implements [CI-TEST-SHARED-CASE-MATRIX].
    private static void ExerciseProjectionCaseMatrix9(string value2)
    {
        var result = RlsPredicateTranspiler.Translate("true", RlsPlatform.Postgres, value2);
        Assert.True(result is TranspileOk);
        Assert.Equal("true", (Ok(actual: result)).Value);
    }
}

/// <summary>
/// Bridge to the internal sentinel constant for assertions.
/// </summary>
internal static class RlsPredicateTranspilerTestAccess
{
    public const string Sentinel = "__RLS_CURRENT_USER_ID__";
}
