// Implements [MIG-TEST-SCHEMA-FIXTURES].
namespace Nimblesite.DataProvider.Migration.Tests;

public sealed partial class RlsPolicyFunctionArgumentDriftTests
{
    [Fact]
    public void QualifiedFunctionName_DoesNotHideChangedStringArgument()
    {
        var current = PolicySchema("public.is_member('public.is_member')");
        var desired = PolicySchema("is_member('is_member')");

        var operations = SchemaDiffAssertions.Diff(current: current, desired: desired);

        Assert.Contains(operations, operation => operation is AlterRlsPolicyOperation);
    }
}
