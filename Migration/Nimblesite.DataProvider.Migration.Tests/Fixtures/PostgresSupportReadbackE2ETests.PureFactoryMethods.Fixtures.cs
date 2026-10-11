// Implements [CI-TEST-INDEPENDENT-SCHEMA-DATA].
// Pure immutable fixture definitions; assertions and database workflows remain analysed.

namespace Nimblesite.DataProvider.Migration.Tests;

public sealed partial class PostgresSupportReadbackE2ETests
{
    private static SchemaDefinition SupportSchema() =>
        SchemaTestData.Schema(
            name: "support_readback",
            roles:
            [
                SchemaTestData.PostgresRole(name: "nap_app_user", login: false, bypassRls: false),
            ],
            functions:
            [
                SchemaTestData.PostgresFunction(
                    schema: "public",
                    name: "current_tenant_id",
                    returns: "uuid",
                    language: "sql",
                    volatility: "stable",
                    securityDefiner: true,
                    body: "SELECT current_setting('app.tenant_id', true)::uuid",
                    executeRoles: ["nap_app_user"],
                    revokePublicExecute: true
                ),
            ],
            grants:
            [
                SchemaTestData.PostgresGrant(
                    schema: "public",
                    target: PostgresGrantTarget.Schema,
                    privileges: ["USAGE"],
                    roles: ["nap_app_user"]
                ),
                SchemaTestData.PostgresGrant(
                    schema: "public",
                    target: PostgresGrantTarget.Table,
                    objectName: "documents",
                    privileges: ["SELECT"],
                    roles: ["nap_app_user"]
                ),
            ],
            tables:
            [
                SchemaTestData.Table(
                    schema: "public",
                    name: "documents",
                    columns:
                    [
                        SchemaTestData.Column(name: "id", type: new UuidType(), isNullable: false),
                    ],
                    primaryKey: new PrimaryKeyDefinition { Columns = ["id"] }
                ),
            ]
        );
}
