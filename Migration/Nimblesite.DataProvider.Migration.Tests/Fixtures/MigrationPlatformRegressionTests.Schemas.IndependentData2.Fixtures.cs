// Implements [CI-DESLOP-INDEPENDENT-DATA].

namespace Nimblesite.DataProvider.Migration.Tests;

public partial record MigrationPlatformRegressionTests
{
    private static global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Dataset_ForeignKeySchema_1(
        global::System.Boolean parent,
        global::Nimblesite.DataProvider.Migration.Core.TableDefinition endUser,
        global::Nimblesite.DataProvider.Migration.Core.TableDefinition events
    ) => SchemaTestData.Schema(name: "issues79_80", tables: parent ? [endUser, events] : [events]);
}
