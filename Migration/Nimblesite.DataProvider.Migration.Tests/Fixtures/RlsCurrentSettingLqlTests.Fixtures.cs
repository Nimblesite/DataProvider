// Implements [CI-DESLOP-INDEPENDENT-DATA].
using TranspileError = Outcome.Result<
    string,
    Nimblesite.DataProvider.Migration.Core.MigrationError
>.Error<string, Nimblesite.DataProvider.Migration.Core.MigrationError>;
using TranspileOk = Outcome.Result<
    string,
    Nimblesite.DataProvider.Migration.Core.MigrationError
>.Ok<string, Nimblesite.DataProvider.Migration.Core.MigrationError>;

namespace Nimblesite.DataProvider.Migration.Tests;

public partial class RlsCurrentSettingLqlTests
{
    private global::Nimblesite.DataProvider.Migration.Core.RlsPolicyDefinition Fixture_Generate_RlsPolicy_CurrentSetting_UsesPostgresMissingOkArgument_1(
        global::System.String name,
        global::System.Collections.Generic.IReadOnlyList<global::System.String>? roles,
        global::System.String? usingLql,
        global::System.String? withCheckLql
    ) =>
        SchemaTestData.RlsPolicy(
            name: "tenant_member",
            roles: ["app_user"],
            usingLql: "tenant_id = current_setting('app.tenant_id')::uuid",
            withCheckLql: "tenant_id = current_setting('app.tenant_id')::uuid"
        );
}
