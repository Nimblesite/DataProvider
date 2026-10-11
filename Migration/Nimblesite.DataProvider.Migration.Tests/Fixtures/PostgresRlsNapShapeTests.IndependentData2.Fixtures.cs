// Implements [CI-DESLOP-INDEPENDENT-DATA].

namespace Nimblesite.DataProvider.Migration.Tests;

public partial class PostgresRlsNapShapeTests
{
    private static global::Nimblesite.DataProvider.Migration.Core.RlsPolicySetDefinition Dataset_NapShape_Drift_ForwardOnly_DoesNotDropOrphan_1(
        global::Nimblesite.DataProvider.Migration.Core.TableDefinition t
    ) =>
        SchemaTestData.RlsPolicySet(
            enabled: true,
            forced: true,
            policies: [t.RowLevelSecurity!.Policies[0]]
        );
}
