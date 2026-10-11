using System.Collections.Immutable;
// Implements [MIG-TEST-SCHEMA-FIXTURES].

using SchemaIntegrityOk = Outcome.Result<
    System.Collections.Immutable.ImmutableArray<string>,
    Nimblesite.DataProvider.Migration.Core.MigrationError
>.Ok<
    System.Collections.Immutable.ImmutableArray<string>,
    Nimblesite.DataProvider.Migration.Core.MigrationError
>;

namespace Nimblesite.DataProvider.Migration.Tests;

/// <summary>
/// Implements [MIG-VERIFY-DEFAULTS-AND-GRANTS]: the schema integrity verifier
/// must accept any live schema that satisfies the desired one, regardless of
/// how the per-provider inspector represents it. Closes #59:
///   1. A platform may read back a string default with an explicit type suffix
///      (e.g. <c>'pending'::text</c>) while YAML declares the bare literal
///      <c>'pending'</c>.
///   2. An inspector may emit grants in atomic per-role form, while YAML can
///      list multiple roles per grant.
///   3. An inspector may not emit <c>AllTablesInSchema</c> directly even when
///      YAML declares it.
/// </summary>
public sealed partial class SchemaIntegrityVerifierIssue59Tests
{
    [Fact]
    public void Verify_TextDefaultStoredWithTypeCast_TreatedAsEqualToBareLiteral()
    {
        var live = Fixture_Verify_TextDefaultStoredWithTypeCast_TreatedAsEqualToBareLiteral_1();
        var desired = Fixture_Verify_TextDefaultStoredWithTypeCast_TreatedAsEqualToBareLiteral_2();

        var mismatches = Verify(live: live, desired: desired);

        Assert.Empty(mismatches);

        var changedDefault =
            Fixture_Verify_TextDefaultStoredWithTypeCast_TreatedAsEqualToBareLiteral_3();

        var changedDefaultMismatches = Verify(live: live, desired: changedDefault);

        Assert.Single(changedDefaultMismatches);
        Assert.Contains("public.t.status: default expected 'failed'", changedDefaultMismatches[0]);
        Assert.DoesNotContain("default expected 'pending'", changedDefaultMismatches[0]);
    }

    [Fact]
    public void Verify_MultiRoleGrantInDesired_MatchedAgainstSingleRoleGrantsFromInspector()
    {
        var live =
            Fixture_Verify_MultiRoleGrantInDesired_MatchedAgainstSingleRoleGrantsFromInspector_1();
        var desired =
            Fixture_Verify_MultiRoleGrantInDesired_MatchedAgainstSingleRoleGrantsFromInspector_2();

        var mismatches = Verify(live: live, desired: desired);

        Assert.Empty(mismatches);

        var missingAdminGrant =
            Dataset_Verify_MultiRoleGrantInDesired_MatchedAgainstSingleRoleGrantsFromInspector_1(
                live: live
            );

        var missingAdminMismatches = Verify(live: missingAdminGrant, desired: desired);

        Assert.Single(missingAdminMismatches);
        Assert.Contains("grant Schema", missingAdminMismatches[0]);
        Assert.Contains("missing grant", missingAdminMismatches[0]);
    }

    [Fact]
    public void Verify_AllTablesInSchemaTarget_MatchedAgainstPerTableGrantsFromInspector()
    {
        var live =
            Fixture_Verify_AllTablesInSchemaTarget_MatchedAgainstPerTableGrantsFromInspector_1();
        var desired =
            Fixture_Verify_AllTablesInSchemaTarget_MatchedAgainstPerTableGrantsFromInspector_2();

        var mismatches = Verify(live: live, desired: desired);

        Assert.Empty(mismatches);

        var missingTableGrant =
            Dataset_Verify_AllTablesInSchemaTarget_MatchedAgainstPerTableGrantsFromInspector_1(
                live: live
            );

        var missingTableMismatches = Verify(live: missingTableGrant, desired: desired);

        Assert.Single(missingTableMismatches);
        Assert.Contains("grant AllTablesInSchema", missingTableMismatches[0]);
        Assert.Contains("missing grant", missingTableMismatches[0]);
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
