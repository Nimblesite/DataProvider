// Implements [CI-TEST-INDEPENDENT-SCHEMA-DATA].
// Pure immutable fixture definitions; assertions and database workflows remain analysed.

namespace Nimblesite.DataProvider.Migration.Tests;

public sealed partial class SchemaDiffCheckConstraintIssue57Tests
{
    private static TableDefinition BaseUsageEventsTable() =>
        SchemaTestData.Table(
            schema: "public",
            name: "usage_events",
            columns:
            [
                SchemaTestData.Column(name: "id", type: PortableTypes.Uuid, isNullable: false),
                SchemaTestData.Column(name: "kind", type: PortableTypes.Text, isNullable: false),
            ],
            primaryKey: new PrimaryKeyDefinition { Columns = ["id"] }
        );
}
