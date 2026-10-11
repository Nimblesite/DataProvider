using static global::Tests.Shared.ResultAssertions;

// Implements [MIG-TEST-SCHEMA-FIXTURES].
namespace Nimblesite.DataProvider.Migration.Tests;

public sealed partial class RlsPolicyFunctionArgumentDriftTests
{
    [Fact]
    public void QualifiedFunctionName_DoesNotHideChangedStringArgument()
    {
        var current = PolicySchema("public.is_member('public.is_member')");
        var desired = PolicySchema("is_member('is_member')");

        var operations = (Ok(actual: SchemaDiff.Calculate(current, desired))).Value;

        Assert.Contains(operations, operation => operation is AlterRlsPolicyOperation);
    }
}
