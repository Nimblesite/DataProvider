// Implements [CI-DESLOP-INDEPENDENT-DATA].

namespace Nimblesite.DataProvider.Migration.Tests;

public partial class PostgresLqlOnlyE2ETests
{
    private static global::Nimblesite.DataProvider.Migration.Core.RlsPolicySetDefinition Dataset_LqlOnly_DropPolicy_AllowDestructive_RemovesIt_1() =>
        SchemaTestData.RlsPolicySet(enabled: true, forced: true, policies: []);
}
