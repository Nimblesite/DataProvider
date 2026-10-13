// Implements [CI-TEST-INDEPENDENT-SCHEMA-DATA].
// Pure immutable fixture definitions; assertions and database workflows remain analysed.

namespace Nimblesite.DataProvider.Migration.Tests;

public sealed partial class PostgresRlsNapShapeTests
{
    private static TableDefinition MakeTenantTable(string tableName) =>
        SchemaTestData.Table(
            schema: "public",
            name: tableName,
            columns:
            [
                SchemaTestData.Column(name: "id", type: new UuidType(), isNullable: false),
                SchemaTestData.Column(name: "tenant_id", type: new UuidType(), isNullable: false),
                SchemaTestData.Column(name: "title", type: new VarCharType(200), isNullable: false),
            ],
            primaryKey: new PrimaryKeyDefinition { Columns = ["id"] },
            rowLevelSecurity: SchemaTestData.RlsPolicySet(
                enabled: true,
                forced: true,
                policies:
                [
                    SchemaTestData.RlsPolicy(
                        name: $"{tableName}_member",
                        operations: [RlsOperation.All],
                        roles: ["nap_app_user"],
                        usingLql: "tenant_id = app_tenant_id()",
                        withCheckLql: "tenant_id = app_tenant_id()"
                    ),
                    SchemaTestData.RlsPolicy(
                        name: $"{tableName}_admin_all",
                        operations: [RlsOperation.All],
                        roles: ["nap_app_admin"],
                        usingLql: "true",
                        withCheckLql: "true"
                    ),
                ]
            )
        );
}
