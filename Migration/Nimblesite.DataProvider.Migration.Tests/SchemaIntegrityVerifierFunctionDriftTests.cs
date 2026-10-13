// Implements [MIG-TEST-SCHEMA-FIXTURES].

namespace Nimblesite.DataProvider.Migration.Tests;

public sealed partial class SchemaIntegrityVerifierFunctionDriftTests
{
    [Fact]
    public void Verify_WhenFunctionDefinitionDrifts_ReturnsDetailedMismatches()
    {
        var live = Fixture_Verify_WhenFunctionDefinitionDrifts_ReturnsDetailedMismatches_1();
        var desired = Fixture_Verify_WhenFunctionDefinitionDrifts_ReturnsDetailedMismatches_2();

        var mismatches = SchemaIntegrityVerifierIssue59Tests.Verify(live: live, desired: desired);

        VerifyReusableWorkflow2(
            input0: mismatches,
            value0: "public.current_tenant_id(text): returns expected uuid but found text",
            value1: "public.current_tenant_id(text): language expected sql but found plpgsql"
        );
        VerifyReusableWorkflow2(
            input0: mismatches,
            value0: "public.current_tenant_id(text): volatility expected stable but found volatile",
            value1: "public.current_tenant_id(text): security definer expected True but found False"
        );
        Assert.Contains("public.current_tenant_id(text): function body drifted", mismatches);
        Assert.Equal(5, mismatches.Length);
        Assert.Empty(SchemaIntegrityVerifierIssue59Tests.Verify(live: desired, desired: desired));
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    internal static void VerifyReusableWorkflow2(
        System.Collections.Immutable.ImmutableArray<string> input0,
        string value0,
        string value1
    )
    {
        Assert.Contains(value0, input0);
        Assert.Contains(value1, input0);
    }
}
