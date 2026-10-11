// Implements [CI-TEST-INDEPENDENT-SCHEMA-DATA].
// Pure immutable fixture definitions; assertions and database workflows remain analysed.

namespace Nimblesite.DataProvider.Migration.Tests;

public sealed partial class SchemaDiffTests
{
    private static TableDefinition RlsTable(RlsPolicySetDefinition rls) =>
        SchemaTestData.Table(
            schema: "public",
            name: "documents",
            columns:
            [
                SchemaTestData.Column(name: "id", type: PortableTypes.Uuid, isNullable: false),
            ],
            primaryKey: new PrimaryKeyDefinition { Columns = ["id"] },
            rowLevelSecurity: rls
        );
}
