using static global::Tests.Shared.ResultAssertions;
using static global::Tests.Shared.StringAssertions;

// Implements [MIG-TEST-SCHEMA-FIXTURES].

namespace Nimblesite.DataProvider.Migration.Tests;

public sealed partial class RlsCurrentSettingLqlTests
{
    [Theory]
    [MemberData(nameof(Translate_CurrentSetting_NapPolicyShapes_EmitsMissingOkArgumentInlineCases))]
    public void Translate_CurrentSetting_NapPolicyShapes_EmitsMissingOkArgument(
        string lql,
        string keyLiteral
    )
    {
        var sql = Pg(lql);

        Assert.Contains($"current_setting({keyLiteral}, true)", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("__RLS_CURRENT_SETTING_", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void Translate_CurrentSetting_WithUuidCast_KeepsCastTypeUnquoted()
    {
        var sql = Pg("tenant_id = current_setting('app.tenant_id')::uuid");

        VerifyCrossClassChecks2(
            input0: sql,
            value0: "\"tenant_id\"",
            value1: "current_setting('app.tenant_id', true)::uuid",
            value3: "\"uuid\""
        );
    }

    [Fact]
    public void Translate_CurrentSetting_MultipleKeys_RewritesEachCall()
    {
        var sql = Pg(
            "tenant_id = current_setting('app.tenant_id')::uuid and user_id = current_setting('app.user_id')::uuid"
        );

        ContainsAll(
            actual: sql,
            expected:
            [
                "current_setting('app.tenant_id', true)::uuid",
                "current_setting('app.user_id', true)::uuid",
            ],
            comparison: StringComparison.Ordinal
        );
    }

    // Implements [CI-TEST-SHARED-CASE-MATRIX].
    [Theory]
    [MemberData(nameof(IndependentScenarioData16))]
    public void Translate_CurrentSetting_InsideStringLiteral_IsNotRewritten_ScenarioCases(
        string scenarioName,
        System.String scenario0,
        System.String scenario1,
        System.String scenario2
    )
    {
        Assert.False(condition: string.IsNullOrEmpty(value: scenarioName));

        var sql = Pg(scenario0);

        Assert.Contains(scenario1, sql, StringComparison.Ordinal);
        Assert.DoesNotContain(scenario2, sql, StringComparison.Ordinal);
    }

    [Fact]
    public void Translate_CurrentSetting_ExistsPipeline_RewritesSettingsAfterLqlTranspile()
    {
        var sql = Pg(
            Data_Translate_CurrentSetting_ExistsPipeline_RewritesSettingsAfterLqlTranspile_1
        );

        MigrationAssertions.AssertSqlContains(
            sql,
            "EXISTS (",
            "FROM tenant_members",
            "current_setting('app.tenant_id', true)::uuid",
            "current_setting('app.user_id', true)::uuid"
        );
        Assert.DoesNotContain("__RLS_CURRENT_SETTING_", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_RlsPolicy_CurrentSetting_UsesPostgresMissingOkArgument()
    {
        var ddl = PostgresDdlGenerator.Generate(
            Dataset_Generate_RlsPolicy_CurrentSetting_UsesPostgresMissingOkArgument_1()
        );

        Assert.Contains(
            "current_setting('app.tenant_id', true)::uuid",
            ddl,
            StringComparison.Ordinal
        );
        Assert.DoesNotContain("current_setting('app.tenant_id')", ddl, StringComparison.Ordinal);
    }

    // Implements [CI-TEST-SHARED-CASE-MATRIX].
    [Theory]
    [MemberData(nameof(Translate_CurrentSetting_Sqlite_ReturnsUnsupportedError_ContextCasesData))]
    public void Translate_CurrentSetting_Sqlite_ReturnsUnsupportedError_ContextCases(
        string caseName,
        string value0,
        global::Nimblesite.DataProvider.Migration.Core.RlsPlatform value1,
        string value3
    )
    {
        Assert.False(condition: string.IsNullOrEmpty(value: caseName));
        ExerciseProjectionCaseMatrix3(
            input0: VerifyReusableWorkflow11,
            value0: value0,
            value1: value1,
            value3: value3
        );
    }

    private static string Pg(string lql)
    {
        var result = RlsPredicateTranspiler.Translate(lql, RlsPlatform.Postgres, "p");
        Assert.True(
            result is TranspileOk,
            result is TranspileError e ? e.Value.Message : "expected Ok"
        );
        return (Ok(actual: result)).Value;
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    internal static void VerifyCrossClassChecks2(
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
        Assert.DoesNotContain(value3, input0, StringComparison.Ordinal);
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    internal static Outcome.Result<string, MigrationError> VerifyReusableWorkflow11(
        string value0,
        global::Nimblesite.DataProvider.Migration.Core.RlsPlatform value1,
        string value2
    )
    {
        var result = RlsPredicateTranspiler.Translate(value0, value1, value2);

        Assert.True(result is TranspileError);

        return result;
    }

    // Implements [CI-TEST-SHARED-CASE-MATRIX].
    private static void ExerciseProjectionCaseMatrix3(
        Func<string, RlsPlatform, string, Outcome.Result<string, MigrationError>> input0,
        string value0,
        global::Nimblesite.DataProvider.Migration.Core.RlsPlatform value1,
        string value3
    )
    {
        var result = input0(arg1: value0, arg2: value1, arg3: "tenant");
        Assert.Contains(value3, (Error(actual: result)).Value.Message);
    }
}
