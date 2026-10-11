using static global::Tests.Shared.StringAssertions;

// Implements [MIG-TEST-SCHEMA-FIXTURES].
namespace Nimblesite.DataProvider.Migration.Tests;

public sealed partial class PostgresSupportDdlTests
{
    [Fact]
    public void Generate_CreateOrAlterRole_EmitsNoLoginNoBypassRlsAndMembershipGrant()
    {
        var ddl = PostgresDdlGenerator.Generate(
            Dataset_Generate_CreateOrAlterRole_EmitsNoLoginNoBypassRlsAndMembershipGrant_1()
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
            Dataset_Generate_CreateOrReplaceFunction_EmitsSecurityDefinerAndExecuteGrants_1()
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
            Dataset_Generate_GrantPrivileges_EmitsAllTablesInSchemaGrant_1()
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
            Dataset_Generate_GrantPrivileges_WithRunAs_WrapsGrantInLocalRole_1()
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
            Dataset_Generate_RevokePrivileges_EmitsTableRevoke_1()
        );

        Assert.Equal("REVOKE SELECT ON TABLE \"public\".\"documents\" FROM \"app_user\"", ddl);
    }
}
