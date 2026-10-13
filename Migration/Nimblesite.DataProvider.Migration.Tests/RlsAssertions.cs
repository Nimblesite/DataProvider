namespace Nimblesite.DataProvider.Migration.Tests;

// Implements [RLS-DIFF]: shared assertions for tables that must carry an
// enabled RLS policy set with exactly one policy.
internal static class RlsAssertions
{
    internal static RlsPolicySetDefinition EnabledWithSinglePolicy(TableDefinition table)
    {
        var rls = Assert.IsType<RlsPolicySetDefinition>(table.RowLevelSecurity);
        Assert.True(rls.Enabled);
        Assert.Single(rls.Policies);
        return rls;
    }
}
