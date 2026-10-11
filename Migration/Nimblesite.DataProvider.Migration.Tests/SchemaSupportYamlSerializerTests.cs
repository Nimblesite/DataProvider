// Implements [MIG-TEST-SCHEMA-FIXTURES].
namespace Nimblesite.DataProvider.Migration.Tests;

public sealed partial class SchemaSupportYamlSerializerTests
{
    [Fact]
    public void FromYaml_PostgresSupportObjects_Deserializes()
    {
        var yaml = """
            name: nap
            roles:
              - name: app_user
                grantTo: [postgres]
            functions:
              - schema: public
                name: is_member
                arguments:
                  - name: tenant_id
                    type: uuid
                  - name: user_id
                    type: uuid
                returns: boolean
                securityDefiner: true
                body: SELECT true
                executeRoles: [app_user, app_admin]
            grants:
              - schema: public
                target: AllTablesInSchema
                privileges: [SELECT, INSERT, UPDATE, DELETE]
                roles: [app_user, app_admin]
              - schema: auth
                target: Table
                objectName: users
                privileges: [SELECT]
                roles: [app_user]
                runAs: supabase_admin
            tables: []
            """;

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
        var schema = Fixture_ToYaml_PostgresSupportObjects_OmitsSemanticDefaults_1();

        var yaml = SchemaYamlSerializer.ToYaml(schema);

        Assert.Contains("functions:", yaml, StringComparison.Ordinal);
        Assert.Contains("name: app_user_id", yaml, StringComparison.Ordinal);
        Assert.DoesNotContain("language: sql", yaml, StringComparison.Ordinal);
        Assert.DoesNotContain("volatility: stable", yaml, StringComparison.Ordinal);
        Assert.DoesNotContain("revokePublicExecute: true", yaml, StringComparison.Ordinal);
    }

    [Fact]
    public void ToYaml_PostgresGrantRunAs_EmitsRunAs()
    {
        var schema = Fixture_ToYaml_PostgresGrantRunAs_EmitsRunAs_1();

        var yaml = SchemaYamlSerializer.ToYaml(schema);

        Assert.Contains("runAs: supabase_admin", yaml, StringComparison.Ordinal);
    }
}
