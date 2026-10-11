// Implements [CI-TEST-SHARED-CASE-MATRIX].

namespace Nimblesite.DataProvider.Migration.Tests;

public partial class RlsErrorCodesTests
{
    /// <summary>Independent named scenario inputs and expectations.</summary>
    public static IEnumerable<object?[]> RlsLqlParse_IncludesDetail_ContextCasesData()
    {
        {
            yield return
            [
                "RlsLqlParse_IncludesDetail",
                new global::System.Func<
                    global::System.String,
                    global::System.String,
                    global::Nimblesite.DataProvider.Migration.Core.MigrationError
                >(MigrationError.RlsLqlParse),
                "p",
                "syntax at line 1",
                "MIG-E-RLS-LQL-PARSE",
                "syntax at line 1",
            ];
        }
        {
            yield return
            [
                "RlsForceUnsupportedOnPlatform_NamesPlatformAndTable",
                new global::System.Func<
                    global::System.String,
                    global::System.String,
                    global::Nimblesite.DataProvider.Migration.Core.MigrationError
                >(MigrationError.RlsForceUnsupportedOnPlatform),
                "Sqlite",
                "documents",
                "MIG-E-RLS-FORCE-UNSUPPORTED-ON-PLATFORM",
                "documents",
            ];
        }
    }
}
