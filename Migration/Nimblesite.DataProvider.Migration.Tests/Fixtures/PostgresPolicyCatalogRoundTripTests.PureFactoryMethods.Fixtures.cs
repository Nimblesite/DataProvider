// Implements [CI-TEST-INDEPENDENT-SCHEMA-DATA].
// Pure immutable fixture definitions; assertions and database workflows remain analysed.

namespace Nimblesite.DataProvider.Migration.Tests;

public sealed partial class PostgresPolicyCatalogRoundTripTests
{
    private static SchemaDefinition PolicySchema(string secondRole) =>
        SchemaTestData.Schema(
            name: "policy_catalog_roundtrip",
            functions:
            [
                SchemaTestData.PostgresFunction(
                    name: "app_is_system",
                    returns: "text",
                    body: "SELECT 'true'::text"
                ),
            ],
            tables:
            [
                SchemaTestData.Table(
                    name: "parents",
                    columns: [Id()],
                    primaryKey: new PrimaryKeyDefinition { Columns = ["id"] }
                ),
                SchemaTestData.Table(
                    name: "children",
                    columns:
                    [
                        Id(),
                        SchemaTestData.Column(name: "parent_id", type: PortableTypes.Uuid),
                        SchemaTestData.Column(name: "role", type: PortableTypes.Text),
                    ],
                    primaryKey: new PrimaryKeyDefinition { Columns = ["id"] },
                    rowLevelSecurity: new RlsPolicySetDefinition
                    {
                        Policies =
                        [
                            SchemaTestData.RlsPolicy(
                                name: "system_all",
                                operations: [RlsOperation.All],
                                usingLql: "app_is_system() = 'true'",
                                withCheckLql: "app_is_system() = 'true'"
                            ),
                            SchemaTestData.RlsPolicy(
                                name: "parent_policy",
                                operations: [RlsOperation.Select],
                                usingLql: "exists(parents |> filter(fn(p) => p.id = parent_id))"
                            ),
                            SchemaTestData.RlsPolicy(
                                name: "role_policy",
                                operations: [RlsOperation.Select],
                                usingLql: $"role in ('admin', '{secondRole}')"
                            ),
                        ],
                    }
                ),
            ]
        );
}
