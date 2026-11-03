// Implements [MIG-TEST-SCHEMA-FIXTURES].
namespace Nimblesite.DataProvider.Migration.Tests;

public sealed partial class SchemaSupportYamlSerializerTests
{
    [Fact]
    public void FromYaml_PostgresSupportObjects_Deserializes()
    {
        var yaml = Data_FromYaml_PostgresSupportObjects_Deserializes_1;

        var schema = SchemaYamlSerializer.FromYaml(yaml);

        Assert.Single(schema.Roles);
        Assert.Equal("app_user", schema.Roles[0].Name);
        Assert.Single(schema.Functions);
        Assert.Equal("is_member", schema.Functions[0].Name);
        Assert.Equal(2, schema.Functions[0].Arguments.Count);
        Assert.Equal(2, schema.Grants.Count);
        Assert.Equal(PostgresGrantTarget.AllTablesInSchema, schema.Grants[0].Target);
        Assert.Equal("supabase_admin", schema.Grants[1].RunAs);
    }

    [Fact]
    public void ToYaml_PostgresSupportObjects_OmitsSemanticDefaults()
    {
        var yaml =
            global::Nimblesite.DataProvider.Migration.Tests.TriggerYamlSerializerTests.VerifyCrossClassChecks6(
                input0: Fixture_ToYaml_PostgresSupportObjects_OmitsSemanticDefaults_1
            );

        global::Nimblesite.DataProvider.Migration.Tests.RlsCurrentSettingLqlTests.VerifyCrossClassChecks2(
            input0: yaml,
            value0: "functions:",
            value1: "name: app_user_id",
            value3: "language: sql"
        );
        Assert.DoesNotContain("volatility: stable", yaml, StringComparison.Ordinal);
        Assert.DoesNotContain("revokePublicExecute: true", yaml, StringComparison.Ordinal);
    }

    [Fact]
    public void FromYaml_PostgresFunctionOwnerAndSearchPath_NormalizesPinnedCatalog()
    {
        var schema = SchemaYamlSerializer.FromYaml(
            Data_FromYaml_PostgresFunctionOwnerAndSearchPath_NormalizesPinnedCatalog_1
        );

        Assert.Equal("nap_fn_owner", schema.Functions[0].OwnerRole);
        Assert.Equal(["pg_catalog", "registry"], schema.Functions[0].SearchPath);
        Assert.Equal(["pg_catalog"], schema.Functions[1].SearchPath);
        Assert.Null(schema.Functions[2].OwnerRole);
        Assert.Empty(schema.Functions[2].SearchPath);
    }

    [Fact]
    public void ToYaml_PostgresFunctionOwner_EmitsOwnerAndExplicitSchemasOnly()
    {
        var yaml = global::Nimblesite.DataProvider.Migration.Tests.TriggerYamlSerializerTests.VerifyCrossClassChecks6(
            input0: Fixture_ToYaml_PostgresFunctionOwner_EmitsOwnerAndExplicitSchemasOnly_1
        );

        Assert.Contains("ownerRole: nap_fn_owner", yaml, StringComparison.Ordinal);
        Assert.Contains("searchPath:", yaml, StringComparison.Ordinal);
        Assert.Contains("- registry", yaml, StringComparison.Ordinal);
        Assert.DoesNotContain("- pg_catalog", yaml, StringComparison.Ordinal);
    }

    [Fact]
    public void ToYaml_PostgresGrantRunAs_EmitsRunAs()
    {
        var yaml =
            global::Nimblesite.DataProvider.Migration.Tests.TriggerYamlSerializerTests.VerifyCrossClassChecks6(
                input0: Fixture_ToYaml_PostgresGrantRunAs_EmitsRunAs_1
            );

        Assert.Contains("runAs: supabase_admin", yaml, StringComparison.Ordinal);
    }
}
