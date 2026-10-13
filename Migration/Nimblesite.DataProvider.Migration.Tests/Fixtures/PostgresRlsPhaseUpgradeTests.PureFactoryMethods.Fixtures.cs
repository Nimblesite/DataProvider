// Implements [CI-TEST-INDEPENDENT-SCHEMA-DATA].
// Pure immutable fixture definitions; assertions and database workflows remain analysed.

namespace Nimblesite.DataProvider.Migration.Tests;

public sealed partial class PostgresRlsPhaseUpgradeTests
{
    private static SchemaDefinition PolicySchema(bool changed) =>
        SchemaTestData.Schema(
            name: "rls_phase_upgrade",
            tables:
            [
                SchemaTestData.Table(
                    name: "documents",
                    columns:
                    [
                        SchemaTestData.Column(
                            name: "id",
                            type: PortableTypes.Uuid,
                            isNullable: false
                        ),
                        SchemaTestData.Column(name: "owner_id", type: PortableTypes.Uuid),
                    ],
                    primaryKey: new PrimaryKeyDefinition { Columns = ["id"] },
                    rowLevelSecurity: new RlsPolicySetDefinition
                    {
                        Policies =
                        [
                            SchemaTestData.RlsPolicy(
                                name: "owner_policy",
                                operations: [RlsOperation.All],
                                usingSql: changed ? "owner_id IS NULL" : "owner_id IS NOT NULL",
                                withCheckSql: changed ? "owner_id IS NULL" : "owner_id IS NOT NULL"
                            ),
                            SchemaTestData.RlsPolicy(
                                name: "scope_policy",
                                operations: [changed ? RlsOperation.Delete : RlsOperation.Select],
                                usingSql: "TRUE"
                            ),
                        ],
                    }
                ),
            ]
        );
}
