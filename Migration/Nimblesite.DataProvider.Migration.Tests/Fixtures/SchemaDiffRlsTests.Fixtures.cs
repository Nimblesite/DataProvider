// Implements [CI-DESLOP-INDEPENDENT-DATA].

namespace Nimblesite.DataProvider.Migration.Tests;

public partial class SchemaDiffRlsTests
{
    private global::Nimblesite.DataProvider.Migration.Core.RlsPolicySetDefinition Fixture_Diff_NewTableWithRls_EmitsCreateTableThenEnableThenPolicy_1(
        global::System.String name,
        global::System.String? usingLql
    ) =>
        new RlsPolicySetDefinition
        {
            Policies =
            [
                SchemaTestData.RlsPolicy(name: "owner", usingLql: "Id = current_user_id()"),
            ],
        };

    private global::Nimblesite.DataProvider.Migration.Core.RlsPolicySetDefinition Fixture_Diff_ExistingTableNoRls_DesiredAddsPolicy_EmitsEnableAndCreate_1(
        global::System.String name,
        global::System.String? usingLql
    ) =>
        new RlsPolicySetDefinition
        {
            Policies =
            [
                SchemaTestData.RlsPolicy(name: "owner", usingLql: "Id = current_user_id()"),
            ],
        };
}
