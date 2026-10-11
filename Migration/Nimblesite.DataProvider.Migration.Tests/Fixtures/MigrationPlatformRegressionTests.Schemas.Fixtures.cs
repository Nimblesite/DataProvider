// Implements [CI-DESLOP-INDEPENDENT-DATA].


namespace Nimblesite.DataProvider.Migration.Tests;

public partial class MigrationPlatformRegressionTests
{
    private static global::Nimblesite.DataProvider.Migration.Core.TableDefinition Fixture_ForeignKeySchema_1() =>
        SchemaTestData.Table(
            name: "tenant_end_users",
            columns:
            [
                SchemaTestData.Column(name: "id", type: PortableTypes.Uuid, isNullable: false),
            ],
            primaryKey: new PrimaryKeyDefinition { Columns = ["id"] }
        );
}
