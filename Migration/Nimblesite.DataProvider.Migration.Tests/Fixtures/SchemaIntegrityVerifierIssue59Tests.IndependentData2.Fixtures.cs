// Implements [CI-DESLOP-INDEPENDENT-DATA].

namespace Nimblesite.DataProvider.Migration.Tests;

public partial class SchemaIntegrityVerifierIssue59Tests
{
    private static global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Dataset_Verify_MultiRoleGrantInDesired_MatchedAgainstSingleRoleGrantsFromInspector_1(
        global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition live
    ) => SchemaTestData.Schema(name: "missing-admin-grant", grants: [live.Grants[0]]);

    private static global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Dataset_Verify_AllTablesInSchemaTarget_MatchedAgainstPerTableGrantsFromInspector_1(
        global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition live
    ) =>
        SchemaTestData.Schema(
            name: "missing-table-grant",
            tables: live.Tables,
            grants: [live.Grants[0]]
        );
}
