// Implements [MIG-TEST-SCHEMA-FIXTURES].
using System.Collections.Immutable;
using SchemaIntegrityOk = Outcome.Result<
    System.Collections.Immutable.ImmutableArray<string>,
    Nimblesite.DataProvider.Migration.Core.MigrationError
>.Ok<
    System.Collections.Immutable.ImmutableArray<string>,
    Nimblesite.DataProvider.Migration.Core.MigrationError
>;

namespace Nimblesite.DataProvider.Migration.Tests;

public sealed partial class SchemaIntegrityVerifierFunctionDriftTests
{
    [Fact]
    public void Verify_WhenFunctionDefinitionDrifts_ReturnsDetailedMismatches()
    {
        var live = Fixture_Verify_WhenFunctionDefinitionDrifts_ReturnsDetailedMismatches_1();
        var desired = Fixture_Verify_WhenFunctionDefinitionDrifts_ReturnsDetailedMismatches_2();

        var mismatches = Verify(live: live, desired: desired);

        Assert.Contains(
            "public.current_tenant_id(text): returns expected uuid but found text",
            mismatches
        );
        Assert.Contains(
            "public.current_tenant_id(text): language expected sql but found plpgsql",
            mismatches
        );
        Assert.Contains(
            "public.current_tenant_id(text): volatility expected stable but found volatile",
            mismatches
        );
        Assert.Contains(
            "public.current_tenant_id(text): security definer expected True but found False",
            mismatches
        );
        Assert.Contains("public.current_tenant_id(text): function body drifted", mismatches);
        Assert.Equal(5, mismatches.Length);
        Assert.Empty(Verify(live: desired, desired: desired));
    }

    private static ImmutableArray<string> Verify(SchemaDefinition live, SchemaDefinition desired)
    {
        var result = SchemaIntegrityVerifier.Verify(
            live: live,
            desired: desired,
            logger: NullLogger.Instance
        );

        Assert.True(
            result is SchemaIntegrityOk,
            "Expected schema integrity verification to succeed."
        );
        return ((SchemaIntegrityOk)result).Value;
    }
}
