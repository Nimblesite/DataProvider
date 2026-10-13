// Implements [CI-DESLOP-INDEPENDENT-DATA].

namespace Nimblesite.DataProvider.Migration.Tests;

public partial class RlsCurrentSettingLqlTests
{
    private static global::Nimblesite.DataProvider.Migration.Core.CreateRlsPolicyOperation Dataset_Generate_RlsPolicy_CurrentSetting_UsesPostgresMissingOkArgument_1() =>
        new CreateRlsPolicyOperation(
            "public",
            "agent_configs",
            Fixture_Generate_RlsPolicy_CurrentSetting_UsesPostgresMissingOkArgument_1()
        );
}
