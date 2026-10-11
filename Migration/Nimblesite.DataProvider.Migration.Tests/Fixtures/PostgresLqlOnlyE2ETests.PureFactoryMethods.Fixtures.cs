// Implements [CI-TEST-INDEPENDENT-SCHEMA-DATA].
// Pure immutable fixture definitions; assertions and database workflows remain analysed.

namespace Nimblesite.DataProvider.Migration.Tests;

public sealed partial class PostgresLqlOnlyE2ETests
{
    private static SchemaDefinition TenantMembersSchema() =>
        SchemaTestData.Schema(
            name: "lql",
            tables:
            [
                SchemaTestData.Table(
                    schema: "public",
                    name: "tenant_members",
                    columns:
                    [
                        SchemaTestData.Column(name: "id", type: new UuidType(), isNullable: false),
                        SchemaTestData.Column(
                            name: "user_id",
                            type: new UuidType(),
                            isNullable: false
                        ),
                        SchemaTestData.Column(
                            name: "tenant_id",
                            type: new UuidType(),
                            isNullable: false
                        ),
                    ],
                    primaryKey: new PrimaryKeyDefinition { Columns = ["id"] }
                ),
            ]
        );

    private static SchemaDefinition TenantTableLqlOnly(string name) =>
        SchemaTestData.Schema(
            name: "lql",
            tables:
            [
                SchemaTestData.Table(
                    schema: "public",
                    name: name,
                    columns:
                    [
                        SchemaTestData.Column(name: "id", type: new UuidType(), isNullable: false),
                        SchemaTestData.Column(
                            name: "tenant_id",
                            type: new UuidType(),
                            isNullable: false
                        ),
                        SchemaTestData.Column(
                            name: "title",
                            type: new VarCharType(200),
                            isNullable: false
                        ),
                    ],
                    primaryKey: new PrimaryKeyDefinition { Columns = ["id"] },
                    rowLevelSecurity: SchemaTestData.RlsPolicySet(
                        enabled: true,
                        forced: true,
                        policies:
                        [
                            SchemaTestData.RlsPolicy(
                                name: $"{name}_member",
                                operations: [RlsOperation.All],
                                roles: ["lql_user"],
                                usingLql: "tenant_id = app_tenant_id() and is_member(app_user_id(), app_tenant_id())",
                                withCheckLql: "tenant_id = app_tenant_id() and is_member(app_user_id(), app_tenant_id())"
                            ),
                            SchemaTestData.RlsPolicy(
                                name: $"{name}_admin_all",
                                operations: [RlsOperation.All],
                                roles: ["lql_admin"],
                                usingLql: "true",
                                withCheckLql: "true"
                            ),
                        ]
                    )
                ),
            ]
        );
}
