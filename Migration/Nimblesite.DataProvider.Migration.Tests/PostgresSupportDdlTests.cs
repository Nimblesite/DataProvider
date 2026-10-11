// Implements [MIG-TEST-SCHEMA-FIXTURES].
namespace Nimblesite.DataProvider.Migration.Tests;

public sealed class PostgresSupportDdlTests
{
    [Fact]
    public void Generate_CreateOrAlterRole_EmitsNoLoginNoBypassRlsAndMembershipGrant()
    {
        var ddl = PostgresDdlGenerator.Generate(
            new CreateOrAlterRoleOperation(
                SchemaTestData.PostgresRole(name: "app_user", grantTo: ["postgres"])
            )
        );

        Assert.Contains("CREATE ROLE \"app_user\" NOLOGIN NOBYPASSRLS;", ddl);
        Assert.Contains("ALTER ROLE \"app_user\" NOLOGIN NOBYPASSRLS", ddl);
        Assert.Contains("GRANT \"app_user\" TO \"postgres\"", ddl);
    }

    [Fact]
    public void Generate_CreateOrReplaceFunction_EmitsSecurityDefinerAndExecuteGrants()
    {
        var ddl = PostgresDdlGenerator.Generate(
            new CreateOrReplaceFunctionOperation(
                SchemaTestData.PostgresFunction(
                    schema: "public",
                    name: "is_member",
                    arguments:
                    [
                        SchemaTestData.PostgresFunctionArgument(name: "tenant_id", type: "uuid"),
                        SchemaTestData.PostgresFunctionArgument(name: "user_id", type: "uuid"),
                    ],
                    returns: "boolean",
                    securityDefiner: true,
                    body: "SELECT true",
                    executeRoles: ["app_user", "app_admin"]
                )
            )
        );

        Assert.Contains(
            "CREATE OR REPLACE FUNCTION \"public\".\"is_member\"(\"tenant_id\" uuid, \"user_id\" uuid)",
            ddl,
            StringComparison.Ordinal
        );
        Assert.Contains("RETURNS boolean", ddl, StringComparison.Ordinal);
        Assert.Contains("LANGUAGE sql", ddl, StringComparison.Ordinal);
        Assert.Contains("STABLE", ddl, StringComparison.Ordinal);
        Assert.Contains("SECURITY DEFINER", ddl, StringComparison.Ordinal);
        Assert.Contains(
            "REVOKE EXECUTE ON FUNCTION \"public\".\"is_member\"(uuid, uuid) FROM PUBLIC",
            ddl,
            StringComparison.Ordinal
        );
        Assert.Contains(
            "GRANT EXECUTE ON FUNCTION \"public\".\"is_member\"(uuid, uuid) TO \"app_user\", \"app_admin\"",
            ddl,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void Generate_GrantPrivileges_EmitsAllTablesInSchemaGrant()
    {
        var ddl = PostgresDdlGenerator.Generate(
            new GrantPrivilegesOperation(
                SchemaTestData.PostgresGrant(
                    schema: "public",
                    target: PostgresGrantTarget.AllTablesInSchema,
                    privileges: ["select", "insert", "update", "delete"],
                    roles: ["app_user", "app_admin"]
                )
            )
        );

        Assert.Equal(
            "GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA \"public\" TO \"app_user\", \"app_admin\"",
            ddl
        );
    }

    [Fact]
    public void Generate_GrantPrivileges_WithRunAs_WrapsGrantInLocalRole()
    {
        var ddl = PostgresDdlGenerator.Generate(
            new GrantPrivilegesOperation(
                SchemaTestData.PostgresGrant(
                    schema: "auth",
                    target: PostgresGrantTarget.Table,
                    objectName: "users",
                    privileges: ["select", "insert"],
                    roles: ["app_admin"],
                    runAs: "supabase_admin"
                )
            )
        );

        Assert.Contains("pg_has_role(current_user, 'supabase_admin', 'MEMBER')", ddl);
        Assert.Contains("MIG-E-PG-GRANT-RUN-AS-MISSING-MEMBERSHIP", ddl);
        Assert.Contains("GRANT \"supabase_admin\" TO", ddl, StringComparison.Ordinal);
        Assert.Contains("SET LOCAL ROLE \"supabase_admin\";", ddl, StringComparison.Ordinal);
        Assert.Contains(
            "GRANT SELECT, INSERT ON TABLE \"auth\".\"users\" TO \"app_admin\";",
            ddl,
            StringComparison.Ordinal
        );
        Assert.EndsWith("RESET ROLE", ddl, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_RevokePrivileges_EmitsTableRevoke()
    {
        var ddl = PostgresDdlGenerator.Generate(
            new RevokePrivilegesOperation(
                SchemaTestData.PostgresGrant(
                    schema: "public",
                    target: PostgresGrantTarget.Table,
                    objectName: "documents",
                    privileges: ["select"],
                    roles: ["app_user"]
                )
            )
        );

        Assert.Equal("REVOKE SELECT ON TABLE \"public\".\"documents\" FROM \"app_user\"", ddl);
    }
}
