// Implements [CI-TEST-INDEPENDENT-SCHEMA-DATA].
// Pure immutable fixture definitions; assertions and database workflows remain analysed.

namespace Nimblesite.DataProvider.Migration.Tests;

public sealed partial class RlsPolicyFunctionArgumentDriftTests
{
    private static SchemaDefinition PolicySchema(string predicate) =>
        SchemaTestData.Schema(
            name: "rls_function_argument_drift",
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
                    ],
                    primaryKey: new PrimaryKeyDefinition { Columns = ["id"] },
                    rowLevelSecurity: new RlsPolicySetDefinition
                    {
                        Policies =
                        [
                            SchemaTestData.RlsPolicy(
                                name: "documents_member",
                                operations: [RlsOperation.Select],
                                usingSql: predicate
                            ),
                        ],
                    }
                ),
            ]
        );
}
