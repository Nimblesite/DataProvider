// Implements [CI-TEST-SHARED-CASE-MATRIX].
namespace Nimblesite.DataProvider.Migration.Tests;

public partial class MigrationCornerCaseTests
{
    /// <summary>Independent named inputs for the shared scenario runner.</summary>
    public static System.Collections.Generic.IEnumerable<object?[]> IndependentCommentScenarioData4()
    {
        {
            global::System.Func<global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition> scenario0 =
                Fixture_Table_MultiColumnUniqueConstraint_Success_1;
            yield return ["Table_MultiColumnUniqueConstraint_Success", scenario0];
        }
        {
            global::System.Func<global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition> scenario0 =
                Fixture_Table_MultipleIdentityColumns_OnePerTable_1;
            yield return ["Table_MultipleIdentityColumns_OnePerTable", scenario0];
        }
    }
}
