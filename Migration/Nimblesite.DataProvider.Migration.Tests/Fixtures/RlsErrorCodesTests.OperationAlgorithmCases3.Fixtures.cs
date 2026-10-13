// Implements [CI-TEST-SHARED-CASE-MATRIX].

namespace Nimblesite.DataProvider.Migration.Tests;

public partial class RlsErrorCodesTests
{
    /// <summary>Independent named scenario inputs and expectations.</summary>
    public static IEnumerable<object?[]> RlsEmptyPredicate_IncludesPolicyName_ContextCasesData()
    {
        {
            yield return
            [
                "RlsEmptyPredicate_IncludesPolicyName",
                new global::System.Func<
                    global::System.String,
                    global::Nimblesite.DataProvider.Migration.Core.MigrationError
                >(MigrationError.RlsEmptyPredicate),
                "MIG-E-RLS-EMPTY-PREDICATE",
            ];
        }
        {
            yield return
            [
                "RlsEmptyCheck_IncludesPolicyName",
                new global::System.Func<
                    global::System.String,
                    global::Nimblesite.DataProvider.Migration.Core.MigrationError
                >(MigrationError.RlsEmptyCheck),
                "MIG-E-RLS-EMPTY-CHECK",
            ];
        }
    }
}
