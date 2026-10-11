// Implements [CI-TEST-SHARED-CASE-MATRIX].
namespace Nimblesite.DataProvider.Migration.Tests;

public partial class RlsCurrentSettingLqlTests
{
    /// <summary>Independent input and expected-value rows for the named scenarios.</summary>
    public static System.Collections.Generic.IEnumerable<object?[]> IndependentScenarioData16()
    {
        {
            yield return
            [
                "Translate_CurrentSetting_InsideStringLiteral_IsNotRewritten",
                "note = 'current_setting(''app.tenant_id'')'",
                "'current_setting(''app.tenant_id'')'",
                ", true",
            ];
        }
        {
            yield return
            [
                "Translate_CurrentSetting_ExistsPipeline_FnCallArg_RewritesSetting",
                Data_Translate_CurrentSetting_ExistsPipeline_FnCallArg_RewritesSetting_1,
                "is_member(current_setting('app.user_id', true)::uuid",
                "__RLS_CURRENT_SETTING_",
            ];
        }
    }
}
