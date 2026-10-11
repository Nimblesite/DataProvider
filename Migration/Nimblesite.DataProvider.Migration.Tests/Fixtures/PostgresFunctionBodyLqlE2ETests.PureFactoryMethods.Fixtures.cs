// Implements [CI-TEST-INDEPENDENT-SCHEMA-DATA].
// Pure immutable fixture definitions; assertions and database workflows remain analysed.

namespace Nimblesite.DataProvider.Migration.Tests;

public sealed partial class PostgresFunctionBodyLqlE2ETests
{
    private static SchemaDefinition Schema(Names names) =>
        SchemaTestData.Schema(
            name: "body_lql",
            roles: [SchemaTestData.PostgresRole(name: names.AppUserRole, grantTo: ["test"])],
            tables: [TenantMembersTable(), DocumentsTable(names)],
            functions:
            [
                SchemaTestData.PostgresFunction(
                    name: names.AppTenantFunction,
                    returns: "uuid",
                    bodyLql: "current_setting('app.tenant_id')::uuid",
                    executeRoles: [names.AppUserRole]
                ),
                SchemaTestData.PostgresFunction(
                    name: names.AppUserFunction,
                    returns: "uuid",
                    bodyLql: "current_setting('app.user_id')::uuid",
                    executeRoles: [names.AppUserRole]
                ),
                SchemaTestData.PostgresFunction(
                    name: names.IsMemberFunction,
                    returns: "boolean",
                    arguments:
                    [
                        SchemaTestData.PostgresFunctionArgument(name: "u", type: "uuid"),
                        SchemaTestData.PostgresFunctionArgument(name: "t", type: "uuid"),
                    ],
                    securityDefiner: true,
                    bodyLql: "exists(tenant_members |> filter(fn(m) => m.user_id = u and m.tenant_id = t))",
                    executeRoles: [names.AppUserRole]
                ),
            ],
            grants:
            [
                SchemaTestData.PostgresGrant(
                    schema: "public",
                    target: PostgresGrantTarget.Schema,
                    privileges: ["USAGE"],
                    roles: [names.AppUserRole]
                ),
                SchemaTestData.PostgresGrant(
                    schema: "public",
                    target: PostgresGrantTarget.AllTablesInSchema,
                    privileges: ["SELECT", "INSERT", "UPDATE", "DELETE"],
                    roles: [names.AppUserRole]
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
            ],
            primaryKey: new PrimaryKeyDefinition { Columns = ["id"] }
        );

    private static TableDefinition DocumentsTable(Names names) =>
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
                        usingLql: $"tenant_id = {names.AppTenantFunction}() and {names.IsMemberFunction}({names.AppUserFunction}(), {names.AppTenantFunction}())",
                        withCheckLql: $"tenant_id = {names.AppTenantFunction}() and {names.IsMemberFunction}({names.AppUserFunction}(), {names.AppTenantFunction}())"
                    ),
                ]
            )
        );
}
