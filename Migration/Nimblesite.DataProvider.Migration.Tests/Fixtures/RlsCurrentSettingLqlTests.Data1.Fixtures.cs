// Implements [CI-DESLOP-INDEPENDENT-DATA].
namespace Nimblesite.DataProvider.Migration.Tests;

public partial class RlsCurrentSettingLqlTests
{
    private const string Data_Translate_CurrentSetting_ExistsPipeline_RewritesSettingsAfterLqlTranspile_1 = """
        exists(
          tenant_members
          |> filter(fn(m) => m.tenant_id = current_setting('app.tenant_id')::uuid and m.user_id = current_setting('app.user_id')::uuid)
        )
        """;

    private const string Data_Translate_CurrentSetting_ExistsPipeline_FnCallArg_RewritesSetting_1 = """
        exists(
          tenant_members
          |> filter(fn(m) => is_member(current_setting('app.user_id')::uuid, m.tenant_id))
        )
        """;
}
