// Implements [CI-TEST-SHARED-CASE-MATRIX].
namespace Nimblesite.Sync.Tests;

public partial class LqlExpressionEvaluatorTests
{
    /// <summary>Independent named inputs for the shared scenario runner.</summary>
    public static System.Collections.Generic.IEnumerable<object?[]> IndependentInstanceScenarioData1()
    {
        {
            global::System.Func<global::System.Collections.Generic.List<global::Nimblesite.Sync.Core.ColumnMapping>> scenario1 =
                Dataset_MappingEngine_LqlTransform_AppliesUppercase_1;
            yield return
            [
                "MappingEngine_LqlTransform_AppliesUppercase",
                scenario1,
                """{"Id":"u1","Name":"alice"}""",
                "DisplayName",
                "ALICE",
            ];
        }
        {
            global::System.Func<global::System.Collections.Generic.List<global::Nimblesite.Sync.Core.ColumnMapping>> scenario1 =
                Dataset_MappingEngine_LqlTransform_ConcatNamesForDifferentSchema_1;
            yield return
            [
                "MappingEngine_LqlTransform_ConcatNamesForDifferentSchema",
                scenario1,
                """{"Id":"u1","FirstName":"John","LastName":"Doe"}""",
                "FullName",
                "John Doe",
            ];
        }
        {
            global::System.Func<global::System.Collections.Generic.List<global::Nimblesite.Sync.Core.ColumnMapping>> scenario1 =
                Dataset_MappingEngine_LqlTransform_ExtractDomainFromEmail_1;
            yield return
            [
                "MappingEngine_LqlTransform_ExtractDomainFromEmail",
                scenario1,
                """{"Id":"u1","Email":"user@example.com"}""",
                "Domain",
                "example.com",
            ];
        }
    }
}
