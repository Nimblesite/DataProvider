// Implements [CI-TEST-INDEPENDENT-SCHEMA-DATA].
// Pure immutable fixture definitions; assertions and database workflows remain analysed.

namespace Nimblesite.DataProvider.Migration.Tests;

public sealed partial class SchemaDiffSupportTests
{
    private static SchemaDefinition SupportSchema() =>
        SchemaTestData.Schema(
            name: "support",
            roles: [SchemaTestData.PostgresRole(name: "app_user", grantTo: ["postgres"])],
            tables:
            [
                SchemaTestData.Table(
                    schema: "public",
                    name: "documents",
                    columns:
                    [
                        SchemaTestData.Column(
                            name: "id",
                            type: PortableTypes.Uuid,
                            isNullable: false
                        ),
                    ],
                    primaryKey: new PrimaryKeyDefinition { Columns = ["id"] },
                    rowLevelSecurity: new RlsPolicySetDefinition
                    {
                        Policies =
                        [
                            SchemaTestData.RlsPolicy(
                                name: "documents_member",
                                roles: ["app_user"],
                                usingSql: "is_member()",
                                withCheckSql: "is_member()"
                            ),
                        ],
                    }
                ),
            ],
            functions:
            [
                SchemaTestData.PostgresFunction(
                    name: "is_member",
                    returns: "boolean",
                    securityDefiner: true,
                    body: "SELECT true",
                    executeRoles: ["app_user"]
                ),
            ],
            grants:
            [
                SchemaTestData.PostgresGrant(
                    schema: "public",
                    target: PostgresGrantTarget.AllTablesInSchema,
                    privileges: ["SELECT", "INSERT"],
                    roles: ["app_user"]
                ),
            ]
        );
}
