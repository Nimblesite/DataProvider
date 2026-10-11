using static global::Tests.Shared.StringAssertions;
// Implements [MIG-TEST-SCHEMA-FIXTURES].
using TranspileError = Outcome.Result<
    string,
    Nimblesite.DataProvider.Migration.Core.MigrationError
>.Error<string, Nimblesite.DataProvider.Migration.Core.MigrationError>;
using TranspileOk = Outcome.Result<
    string,
    Nimblesite.DataProvider.Migration.Core.MigrationError
>.Ok<string, Nimblesite.DataProvider.Migration.Core.MigrationError>;

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

        ContainsAll(
            actual: sql,
            expected: ["\"tenant_id\"", "current_setting('app.tenant_id', true)::uuid"],
            comparison: StringComparison.Ordinal
        );
        Assert.DoesNotContain("\"uuid\"", sql, StringComparison.Ordinal);
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

    [Fact]
    public void Translate_CurrentSetting_InsideStringLiteral_IsNotRewritten()
    {
        var sql = Pg("note = 'current_setting(''app.tenant_id'')'");

        Assert.Contains("'current_setting(''app.tenant_id'')'", sql, StringComparison.Ordinal);
        Assert.DoesNotContain(", true", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void Translate_CurrentSetting_ExistsPipeline_RewritesSettingsAfterLqlTranspile()
    {
        var sql = Pg(
            Data_Translate_CurrentSetting_ExistsPipeline_RewritesSettingsAfterLqlTranspile_1
        );

        Assert.StartsWith("EXISTS (", sql, StringComparison.Ordinal);
        ContainsAll(
            actual: sql,
            expected:
            [
                "FROM tenant_members",
                "current_setting('app.tenant_id', true)::uuid",
                "current_setting('app.user_id', true)::uuid",
            ],
            comparison: StringComparison.Ordinal
        );
        Assert.DoesNotContain("__RLS_CURRENT_SETTING_", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void Translate_CurrentSetting_ExistsPipeline_FnCallArg_RewritesSetting()
    {
        var sql = Pg(Data_Translate_CurrentSetting_ExistsPipeline_FnCallArg_RewritesSetting_1);

        Assert.Contains(
            "is_member(current_setting('app.user_id', true)::uuid",
            sql,
            StringComparison.Ordinal
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

    [Fact]
    public void Translate_CurrentSetting_Sqlite_ReturnsUnsupportedError()
    {
        var result = RlsPredicateTranspiler.Translate(
            "tenant_id = current_setting('app.tenant_id')::uuid",
            RlsPlatform.Sqlite,
            "tenant"
        );

        Assert.True(result is TranspileError);
        Assert.Contains("supported only for PostgreSQL", ((TranspileError)result).Value.Message);
    }

    [Fact]
    public void Translate_CurrentSetting_NonLiteralArgument_ReturnsParseError()
    {
        var result = RlsPredicateTranspiler.Translate(
            "tenant_id = current_setting(app.tenant_id)::uuid",
            RlsPlatform.Postgres,
            "tenant"
        );

        Assert.True(result is TranspileError);
        Assert.Contains(
            "requires exactly one string-literal argument",
            ((TranspileError)result).Value.Message
        );
    }

    private static string Pg(string lql)
    {
        var result = RlsPredicateTranspiler.Translate(lql, RlsPlatform.Postgres, "p");
        Assert.True(
            result is TranspileOk,
            result is TranspileError e ? e.Value.Message : "expected Ok"
        );
        return ((TranspileOk)result).Value;
    }
}
