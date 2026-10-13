// Implements [CI-DESLOP-INDEPENDENT-DATA].
namespace Nimblesite.DataProvider.Migration.Tests;

public partial class RlsCurrentSettingLqlTests
{
    private static global::Nimblesite.DataProvider.Migration.Core.RlsPolicyDefinition Fixture_Generate_RlsPolicy_CurrentSetting_UsesPostgresMissingOkArgument_1() =>
        SchemaTestData.RlsPolicy(
            name: "tenant_member",
            roles: ["app_user"],
            usingLql: "tenant_id = current_setting('app.tenant_id')::uuid",
            withCheckLql: "tenant_id = current_setting('app.tenant_id')::uuid"
        );
}
