// Implements [CI-DESLOP-INDEPENDENT-DATA].

namespace Nimblesite.DataProvider.Migration.Tests;

public partial class PostgresSupportDdlTests
{
    private static global::Nimblesite.DataProvider.Migration.Core.CreateOrAlterRoleOperation Dataset_Generate_CreateOrAlterRole_EmitsNoLoginNoBypassRlsAndMembershipGrant_1() =>
        new CreateOrAlterRoleOperation(
            SchemaTestData.PostgresRole(name: "app_user", grantTo: ["postgres"])
        );

    private static global::Nimblesite.DataProvider.Migration.Core.CreateOrReplaceFunctionOperation Dataset_Generate_CreateOrReplaceFunction_EmitsSecurityDefinerAndExecuteGrants_1() =>
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
        );

    private static global::Nimblesite.DataProvider.Migration.Core.GrantPrivilegesOperation Dataset_Generate_GrantPrivileges_EmitsAllTablesInSchemaGrant_1() =>
        new GrantPrivilegesOperation(
            SchemaTestData.PostgresGrant(
                schema: "public",
                target: PostgresGrantTarget.AllTablesInSchema,
                privileges: ["select", "insert", "update", "delete"],
                roles: ["app_user", "app_admin"]
            )
        );

    private static global::Nimblesite.DataProvider.Migration.Core.GrantPrivilegesOperation Dataset_Generate_GrantPrivileges_WithRunAs_WrapsGrantInLocalRole_1() =>
        new GrantPrivilegesOperation(
            SchemaTestData.PostgresGrant(
                schema: "auth",
                target: PostgresGrantTarget.Table,
                objectName: "users",
                privileges: ["select", "insert"],
                roles: ["app_admin"],
                runAs: "supabase_admin"
            )
        );

    private static global::Nimblesite.DataProvider.Migration.Core.RevokePrivilegesOperation Dataset_Generate_RevokePrivileges_EmitsTableRevoke_1() =>
        new RevokePrivilegesOperation(
            SchemaTestData.PostgresGrant(
                schema: "public",
                target: PostgresGrantTarget.Table,
                objectName: "documents",
                privileges: ["select"],
                roles: ["app_user"]
            )
        );
}
