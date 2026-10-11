// Implements [CI-DESLOP-INDEPENDENT-DATA].


namespace Nimblesite.DataProvider.Migration.Tests;

public partial class SchemaDiffRlsTests
{
    private static global::Nimblesite.DataProvider.Migration.Core.RlsPolicySetDefinition Fixture_Diff_NewTableWithRls_EmitsCreateTableThenEnableThenPolicy_1() =>
        new RlsPolicySetDefinition
            {
                Policies =
                [
                    SchemaTestData.RlsPolicy(name: "owner", usingLql: "Id = current_user_id()"),
                ],
            };

    private static global::Nimblesite.DataProvider.Migration.Core.RlsPolicySetDefinition Fixture_Diff_ExistingTableNoRls_DesiredAddsPolicy_EmitsEnableAndCreate_1() =>
        new RlsPolicySetDefinition
            {
                Policies =
                [
                    SchemaTestData.RlsPolicy(name: "owner", usingLql: "Id = current_user_id()"),
                ],
            };
}
