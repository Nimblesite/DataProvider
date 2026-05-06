using TranspileError = Outcome.Result<
    string,
    Nimblesite.DataProvider.Migration.Core.MigrationError
>.Error<string, Nimblesite.DataProvider.Migration.Core.MigrationError>;
using TranspileOk = Outcome.Result<
    string,
    Nimblesite.DataProvider.Migration.Core.MigrationError
>.Ok<string, Nimblesite.DataProvider.Migration.Core.MigrationError>;

namespace Nimblesite.DataProvider.Migration.Tests;

public sealed class RlsCurrentSettingLqlTests
{
    [Theory]
    [InlineData("tenant_id = current_setting('app.tenant_id')::uuid", "'app.tenant_id'")]
    [InlineData("user_id = current_setting('app.user_id')::uuid", "'app.user_id'")]
    [InlineData("workspace_id = current_setting('app.workspace_id')::uuid", "'app.workspace_id'")]
    [InlineData("api_key_id = current_setting('app.api_key_id')::uuid", "'app.api_key_id'")]
    [InlineData("role = current_setting('app.role')", "'app.role'")]
    [InlineData(
        "tenant_id = current_setting('request.jwt.claims.tenant_id')::uuid",
        "'request.jwt.claims.tenant_id'"
    )]
    [InlineData("created_by = current_setting('app.user_id')::uuid", "'app.user_id'")]
    [InlineData("updated_by = current_setting('app.user_id')::uuid", "'app.user_id'")]
    [InlineData("owner_id = current_setting('app.user_id')::uuid", "'app.user_id'")]
    [InlineData("actor_id = current_setting('app.user_id')::uuid", "'app.user_id'")]
    [InlineData(
        "tenant_id = current_setting('app.tenant_id')::uuid and is_member(current_setting('app.user_id')::uuid, current_setting('app.tenant_id')::uuid)",
        "'app.tenant_id'"
    )]
    [InlineData(
        "user_id = current_setting('app.user_id')::uuid or is_owner(current_setting('app.user_id')::uuid, current_setting('app.tenant_id')::uuid)",
        "'app.user_id'"
    )]
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

        Assert.Contains("\"tenant_id\"", sql, StringComparison.Ordinal);
        Assert.Contains(
            "current_setting('app.tenant_id', true)::uuid",
            sql,
            StringComparison.Ordinal
        );
        Assert.DoesNotContain("\"uuid\"", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void Translate_CurrentSetting_MultipleKeys_RewritesEachCall()
    {
        var sql = Pg(
            "tenant_id = current_setting('app.tenant_id')::uuid and user_id = current_setting('app.user_id')::uuid"
        );

        Assert.Contains(
            "current_setting('app.tenant_id', true)::uuid",
            sql,
            StringComparison.Ordinal
        );
        Assert.Contains(
            "current_setting('app.user_id', true)::uuid",
            sql,
            StringComparison.Ordinal
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
            """
            exists(
              tenant_members
              |> filter(fn(m) => m.tenant_id = current_setting('app.tenant_id')::uuid and m.user_id = current_setting('app.user_id')::uuid)
            )
            """
        );

        Assert.StartsWith("EXISTS (", sql, StringComparison.Ordinal);
        Assert.Contains("FROM tenant_members", sql, StringComparison.Ordinal);
        Assert.Contains(
            "current_setting('app.tenant_id', true)::uuid",
            sql,
            StringComparison.Ordinal
        );
        Assert.Contains(
            "current_setting('app.user_id', true)::uuid",
            sql,
            StringComparison.Ordinal
        );
        Assert.DoesNotContain("__RLS_CURRENT_SETTING_", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void Translate_CurrentSetting_ExistsPipeline_FnCallArg_RewritesSetting()
    {
        var sql = Pg(
            """
            exists(
              tenant_members
              |> filter(fn(m) => is_member(current_setting('app.user_id')::uuid, m.tenant_id))
            )
            """
        );

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
            new CreateRlsPolicyOperation(
                "public",
                "agent_configs",
                new RlsPolicyDefinition
                {
                    Name = "tenant_member",
                    Roles = ["app_user"],
                    UsingLql = "tenant_id = current_setting('app.tenant_id')::uuid",
                    WithCheckLql = "tenant_id = current_setting('app.tenant_id')::uuid",
                }
            )
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
