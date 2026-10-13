// Implements [CI-TEST-SHARED-CASE-MATRIX].
namespace Nimblesite.DataProvider.Migration.Tests;

public partial class SchemaIntegrityVerifierIssue59Tests
{
    public static IEnumerable<object?[]> Verify_MultiRoleGrantInDesired_MatchedAgainstSingleRoleGrantsFromInspector_CasesData()
    {
        {
            global::System.Func<global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition> input0 =
                Fixture_Verify_MultiRoleGrantInDesired_MatchedAgainstSingleRoleGrantsFromInspector_1;
            global::System.Func<global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition> input1 =
                Fixture_Verify_MultiRoleGrantInDesired_MatchedAgainstSingleRoleGrantsFromInspector_2;
            global::System.Func<
                global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition,
                global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition
            > input2 =
                Dataset_Verify_MultiRoleGrantInDesired_MatchedAgainstSingleRoleGrantsFromInspector_1;
            yield return
            [
                "Verify_MultiRoleGrantInDesired_MatchedAgainstSingleRoleGrantsFromInspector",
                input0,
                input1,
                input2,
                "grant Schema",
            ];
        }
        {
            global::System.Func<global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition> input0 =
                Fixture_Verify_AllTablesInSchemaTarget_MatchedAgainstPerTableGrantsFromInspector_1;
            global::System.Func<global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition> input1 =
                Fixture_Verify_AllTablesInSchemaTarget_MatchedAgainstPerTableGrantsFromInspector_2;
            global::System.Func<
                global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition,
                global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition
            > input2 =
                Dataset_Verify_AllTablesInSchemaTarget_MatchedAgainstPerTableGrantsFromInspector_1;
            yield return
            [
                "Verify_AllTablesInSchemaTarget_MatchedAgainstPerTableGrantsFromInspector",
                input0,
                input1,
                input2,
                "grant AllTablesInSchema",
            ];
        }
    }
}
