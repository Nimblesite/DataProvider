// Implements [CI-DESLOP-INDEPENDENT-DATA].

namespace Nimblesite.DataProvider.Migration.Tests;

public partial class SqliteMigrationTests
{
    private static global::Nimblesite.DataProvider.Migration.Core.TableDefinition Dataset_InvalidSql_ReturnsUsefulError_1() =>
        SchemaTestData.Table(
            schema: "main",
            name: "Bad\"Table",
            columns: [SchemaTestData.Column(name: "Id", type: PortableTypes.Int)]
        );
}
