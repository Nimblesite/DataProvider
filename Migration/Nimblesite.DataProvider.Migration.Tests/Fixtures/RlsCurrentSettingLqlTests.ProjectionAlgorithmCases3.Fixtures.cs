// Implements [CI-TEST-SHARED-CASE-MATRIX].

namespace Nimblesite.DataProvider.Migration.Tests;

public partial class RlsCurrentSettingLqlTests
{
    /// <summary>Independent named scenario inputs and expectations.</summary>
    public static IEnumerable<object?[]> Translate_CurrentSetting_Sqlite_ReturnsUnsupportedError_ContextCasesData()
    {
        {
            yield return
            [
                "Translate_CurrentSetting_Sqlite_ReturnsUnsupportedError",
                "tenant_id = current_setting('app.tenant_id')::uuid",
                RlsPlatform.Sqlite,
                "supported only for PostgreSQL",
            ];
        }
        {
            yield return
            [
                "Translate_CurrentSetting_NonLiteralArgument_ReturnsParseError",
                "tenant_id = current_setting(app.tenant_id)::uuid",
                RlsPlatform.Postgres,
                "requires exactly one string-literal argument",
            ];
        }
    }
}
