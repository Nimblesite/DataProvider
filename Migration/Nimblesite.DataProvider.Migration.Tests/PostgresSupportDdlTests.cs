namespace Nimblesite.DataProvider.Migration.Tests;

public sealed class PostgresSupportDdlTests
{
    [Fact]
    public void Generate_CreateOrAlterRole_EmitsNoLoginNoBypassRlsAndMembershipGrant()
    {
        var ddl = PostgresDdlGenerator.Generate(
            new CreateOrAlterRoleOperation(
                new PostgresRoleDefinition { Name = "app_user", GrantTo = ["postgres"] }
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
                new PostgresFunctionDefinition
                {
                    Schema = "public",
                    Name = "is_member",
                    Arguments =
                    [
                        new PostgresFunctionArgumentDefinition
                        {
                            Name = "tenant_id",
                            Type = "uuid",
                        },
                        new PostgresFunctionArgumentDefinition { Name = "user_id", Type = "uuid" },
                    ],
                    Returns = "boolean",
                    SecurityDefiner = true,
                    Body = "SELECT true",
                    ExecuteRoles = ["app_user", "app_admin"],
                }
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
                new PostgresGrantDefinition
                {
                    Schema = "public",
                    Target = PostgresGrantTarget.AllTablesInSchema,
                    Privileges = ["select", "insert", "update", "delete"],
                    Roles = ["app_user", "app_admin"],
                }
            )
        );

        Assert.Equal(
            "GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA \"public\" TO \"app_user\", \"app_admin\"",
            ddl
        );
    }

    [Fact]
    public void Generate_RevokePrivileges_EmitsTableRevoke()
    {
        var ddl = PostgresDdlGenerator.Generate(
            new RevokePrivilegesOperation(
                new PostgresGrantDefinition
                {
                    Schema = "public",
                    Target = PostgresGrantTarget.Table,
                    ObjectName = "documents",
                    Privileges = ["select"],
                    Roles = ["app_user"],
                }
            )
        );

        Assert.Equal("REVOKE SELECT ON TABLE \"public\".\"documents\" FROM \"app_user\"", ddl);
    }
}
