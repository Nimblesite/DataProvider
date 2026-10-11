// Implements [CI-TEST-SHARED-CASE-MATRIX].

namespace Nimblesite.DataProvider.Migration.Tests;

public partial class PostgresRlsDdlTests
{
    /// <summary>Independent named scenario inputs and expectations.</summary>
    public static IEnumerable<object?[]> Generate_CreateRlsPolicy_EmptyPredicate_ThrowsWithRlsErrorCode_ContextCasesData()
    {
        {
            global::System.Func<global::Nimblesite.DataProvider.Migration.Core.CreateRlsPolicyOperation> input0 =
                Dataset_Generate_CreateRlsPolicy_EmptyPredicate_ThrowsWithRlsErrorCode_1;
            yield return
            [
                "Generate_CreateRlsPolicy_EmptyPredicate_ThrowsWithRlsErrorCode",
                input0,
                "MIG-E-RLS-EMPTY-PREDICATE",
            ];
        }
        {
            global::System.Func<global::Nimblesite.DataProvider.Migration.Core.CreateRlsPolicyOperation> input0 =
                Dataset_Generate_CreateRlsPolicy_EmptyWithCheck_ThrowsWithRlsErrorCode_1;
            yield return
            [
                "Generate_CreateRlsPolicy_EmptyWithCheck_ThrowsWithRlsErrorCode",
                input0,
                "MIG-E-RLS-EMPTY-CHECK",
            ];
        }
    }
}
