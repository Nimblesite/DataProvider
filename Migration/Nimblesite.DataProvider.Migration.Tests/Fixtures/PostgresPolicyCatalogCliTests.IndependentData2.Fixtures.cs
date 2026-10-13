// Implements [CI-DESLOP-INDEPENDENT-DATA].

namespace Nimblesite.DataProvider.Migration.Tests;

public partial class PostgresPolicyCatalogCliTests
{
    private static global::Nimblesite.DataProvider.Migration.Core.AlterRlsPolicyOperation Dataset_RealPredicateDrift_FailsIntegrityAndRollsBackPolicyAlteration_1(
        global::Nimblesite.DataProvider.Migration.Core.RlsPolicyDefinition before
    ) =>
        new AlterRlsPolicyOperation(
            Schema: "public",
            TableName: "items",
            Policy: before with
            {
                UsingSql = "id = 2",
            }
        );
}
