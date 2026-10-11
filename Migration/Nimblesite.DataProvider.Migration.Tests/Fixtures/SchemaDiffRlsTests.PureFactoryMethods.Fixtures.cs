// Implements [CI-TEST-INDEPENDENT-SCHEMA-DATA].
// Pure immutable fixture definitions; assertions and database workflows remain analysed.

namespace Nimblesite.DataProvider.Migration.Tests;

public sealed partial class SchemaDiffRlsTests
{
    private static SchemaDefinition WithRls(
        RlsPolicySetDefinition? rls,
        string schema = "public"
    ) =>
        SchemaTestData.Schema(
            name: "t",
            tables:
            [
                SchemaTestData.Table(
                    schema: schema,
                    name: "Documents",
                    columns: [SchemaTestData.Column(name: "Id", type: new UuidType())],
                    primaryKey: new PrimaryKeyDefinition { Columns = ["Id"] },
                    rowLevelSecurity: rls
                ),
            ]
        );
}
