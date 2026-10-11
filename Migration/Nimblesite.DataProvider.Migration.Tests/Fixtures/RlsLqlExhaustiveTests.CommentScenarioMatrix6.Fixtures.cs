// Implements [CI-TEST-SHARED-CASE-MATRIX].
namespace Nimblesite.DataProvider.Migration.Tests;

public partial class RlsLqlExhaustiveTests
{
    /// <summary>Independent named inputs for the shared scenario runner.</summary>
    public static System.Collections.Generic.IEnumerable<object?[]> IndependentCommentScenarioData6()
    {
        {
            yield return
            [
                "ExistsPipeline_LambdaWithFnCallInAndClause_Parses",
                Data_ExistsPipeline_LambdaWithFnCallInAndClause_Parses_1,
                "messages_member",
            ];
        }
        {
            yield return
            [
                "ExistsPipeline_LambdaWithSecurityDefinerFnAtTopLevel_Parses",
                Data_ExistsPipeline_LambdaWithSecurityDefinerFnAtTopLevel_Parses_1,
                "test",
            ];
        }
    }
}
