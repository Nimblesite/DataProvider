using System.Collections.Immutable;
using SchemaIntegrityOk = Outcome.Result<
    System.Collections.Immutable.ImmutableArray<string>,
    Nimblesite.DataProvider.Migration.Core.MigrationError
>.Ok<
    System.Collections.Immutable.ImmutableArray<string>,
    Nimblesite.DataProvider.Migration.Core.MigrationError
>;

namespace Nimblesite.DataProvider.Migration.Tests;

public sealed class SchemaIntegrityVerifierFunctionDriftTests
{
    [Fact]
    public void Verify_WhenFunctionDefinitionDrifts_ReturnsDetailedMismatches()
    {
        var live = new SchemaDefinition
        {
            Name = "live",
            Functions =
            [
                new PostgresFunctionDefinition
                {
                    Schema = "public",
                    Name = "current_tenant_id",
                    Arguments = [new PostgresFunctionArgumentDefinition { Type = "text" }],
                    Returns = "text",
                    Language = "plpgsql",
                    Volatility = "volatile",
                    SecurityDefiner = false,
                    Body = "select null::text",
                },
            ],
        };
        var desired = new SchemaDefinition
        {
            Name = "desired",
            Functions =
            [
                new PostgresFunctionDefinition
                {
                    Schema = "public",
                    Name = "current_tenant_id",
                    Arguments = [new PostgresFunctionArgumentDefinition { Type = "text" }],
                    Returns = "uuid",
                    Language = "sql",
                    Volatility = "stable",
                    SecurityDefiner = true,
                    Body = "select current_setting('app.tenant_id')::uuid",
                },
            ],
        };

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
