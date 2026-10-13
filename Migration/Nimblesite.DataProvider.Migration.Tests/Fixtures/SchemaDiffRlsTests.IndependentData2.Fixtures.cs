// Implements [CI-DESLOP-INDEPENDENT-DATA].

namespace Nimblesite.DataProvider.Migration.Tests;

public partial class SchemaDiffRlsTests
{
    private static global::Nimblesite.DataProvider.Migration.Core.RlsPolicySetDefinition Dataset_Diff_OrphanPolicy_AllowDestructiveFalse_NoDrop_1() =>
        new RlsPolicySetDefinition
        {
            Policies = [SchemaTestData.RlsPolicy(name: "orphan", usingLql: "true")],
        };

    private static global::Nimblesite.DataProvider.Migration.Core.RlsPolicySetDefinition Dataset_Diff_OrphanPolicy_AllowDestructiveTrue_EmitsDrop_1() =>
        new RlsPolicySetDefinition
        {
            Policies = [SchemaTestData.RlsPolicy(name: "orphan", usingLql: "true")],
        };

    private static global::Nimblesite.DataProvider.Migration.Core.RlsPolicySetDefinition Dataset_Diff_AddSecondPolicy_OnlyEmitsCreateForNewPolicy_1(
        global::Nimblesite.DataProvider.Migration.Core.RlsPolicyDefinition existing
    ) =>
        new RlsPolicySetDefinition
        {
            Policies = [existing, SchemaTestData.RlsPolicy(name: "extra", usingLql: "true")],
        };
}
