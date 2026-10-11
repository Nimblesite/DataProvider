// Implements [CI-TEST-INDEPENDENT-SCHEMA-DATA].
// Pure immutable fixture definitions; assertions and database workflows remain analysed.

namespace Nimblesite.DataProvider.Migration.Tests;

public sealed partial class PostgresSupportE2ETests
{
    private static SchemaDefinition SupportSchema(SupportNames names) =>
        SchemaTestData.Schema(
            name: "support_objects",
            roles:
            [
                SchemaTestData.PostgresRole(name: names.AppUserRole, grantTo: ["test"]),
                SchemaTestData.PostgresRole(name: names.AppAdminRole, grantTo: ["test"]),
            ],
            tables: [TenantMembersTable(), DocumentsTable(names)],
            functions: SupportFunctions(names),
            grants:
            [
                SchemaTestData.PostgresGrant(
                    schema: "public",
                    target: PostgresGrantTarget.Schema,
                    privileges: ["USAGE"],
                    roles: [names.AppUserRole, names.AppAdminRole]
                ),
                SchemaTestData.PostgresGrant(
                    schema: "public",
                    target: PostgresGrantTarget.AllTablesInSchema,
                    privileges: ["SELECT", "INSERT", "UPDATE", "DELETE"],
                    roles: [names.AppUserRole, names.AppAdminRole]
                ),
            ]
        );

    private static TableDefinition TenantMembersTable() =>
        SchemaTestData.Table(
            schema: "public",
            name: "tenant_members",
            columns:
            [
                SchemaTestData.RequiredUuid(name: "id"),
                SchemaTestData.RequiredUuid(name: "tenant_id"),
                SchemaTestData.RequiredUuid(name: "user_id"),
                SchemaTestData.Column(name: "role", type: PortableTypes.Text, isNullable: false),
            ],
            primaryKey: new PrimaryKeyDefinition { Columns = ["id"] }
        );

    private static TableDefinition DocumentsTable(SupportNames names) =>
        SchemaTestData.Table(
            schema: "public",
            name: "documents",
            columns:
            [
                SchemaTestData.RequiredUuid(name: "id"),
                SchemaTestData.RequiredUuid(name: "tenant_id"),
                SchemaTestData.Column(name: "title", type: PortableTypes.Text, isNullable: false),
            ],
            primaryKey: new PrimaryKeyDefinition { Columns = ["id"] },
            rowLevelSecurity: SchemaTestData.RlsPolicySet(
                forced: true,
                policies:
                [
                    SchemaTestData.RlsPolicy(
                        name: "documents_member",
                        roles: [names.AppUserRole],
                        usingSql: $"tenant_id = {names.TenantFunction}() AND {names.MemberFunction}({names.TenantFunction}(), {names.UserFunction}())",
                        withCheckSql: $"tenant_id = {names.TenantFunction}() AND {names.MemberFunction}({names.TenantFunction}(), {names.UserFunction}())"
                    ),
                    SchemaTestData.RlsPolicy(
                        name: "documents_admin",
                        roles: [names.AppAdminRole],
                        usingSql: "true",
                        withCheckSql: "true"
                    ),
                ]
            )
        );

    private static PostgresFunctionDefinition Function(
        string name,
        string returns,
        string body,
        bool securityDefiner,
        SupportNames names
    ) =>
        SchemaTestData.PostgresFunction(
            name: name,
            returns: returns,
            body: body,
            securityDefiner: securityDefiner,
            executeRoles: [names.AppUserRole, names.AppAdminRole]
        );

    private static PostgresFunctionDefinition MembershipFunction(
        string name,
        string roleClause,
        SupportNames names
    ) =>
        Function(
            name,
            "boolean",
            SqlData_MembershipFunction_1(roleClause: roleClause),
            true,
            names
        ) with
        {
            Arguments =
            [
                SchemaTestData.PostgresFunctionArgument(name: "p_tenant_id", type: "uuid"),
                SchemaTestData.PostgresFunctionArgument(name: "p_user_id", type: "uuid"),
            ],
        };
}
