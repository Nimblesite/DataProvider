using static global::Tests.Shared.StringAssertions;

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

        ContainsAll(
            actual: ddl,
            expected:
            [
                "CREATE ROLE \"app_user\" NOLOGIN NOBYPASSRLS;",
                "ALTER ROLE \"app_user\" NOLOGIN NOBYPASSRLS",
                "GRANT \"app_user\" TO \"postgres\"",
            ]
        );
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

        ContainsAll(
            actual: ddl,
            expected:
            [
                "CREATE OR REPLACE FUNCTION \"public\".\"is_member\"(\"tenant_id\" uuid, \"user_id\" uuid)",
                "RETURNS boolean",
                "LANGUAGE sql",
                "STABLE",
                "SECURITY DEFINER",
                "REVOKE EXECUTE ON FUNCTION \"public\".\"is_member\"(uuid, uuid) FROM PUBLIC",
                "GRANT EXECUTE ON FUNCTION \"public\".\"is_member\"(uuid, uuid) TO \"app_user\", \"app_admin\"",
            ],
            comparison: StringComparison.Ordinal
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

        ContainsAll(
            actual: ddl,
            expected:
            [
                "pg_has_role(current_user, 'supabase_admin', 'MEMBER')",
                "MIG-E-PG-GRANT-RUN-AS-MISSING-MEMBERSHIP",
            ]
        );
        ContainsAll(
            actual: ddl,
            expected:
            [
                "GRANT \"supabase_admin\" TO",
                "SET LOCAL ROLE \"supabase_admin\";",
                "GRANT SELECT, INSERT ON TABLE \"auth\".\"users\" TO \"app_admin\";",
            ],
            comparison: StringComparison.Ordinal
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
