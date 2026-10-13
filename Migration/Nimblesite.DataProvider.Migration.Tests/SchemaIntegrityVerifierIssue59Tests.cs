using System.Collections.Immutable;
using static global::Tests.Shared.ResultAssertions;

// Implements [MIG-TEST-SCHEMA-FIXTURES].

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

        Assert.Empty(Verify(live: live, desired: desired));

        var changedDefault =
            Fixture_Verify_TextDefaultStoredWithTypeCast_TreatedAsEqualToBareLiteral_3();

        var changedDefaultMismatches = Verify(live: live, desired: changedDefault);

        Assert.Single(changedDefaultMismatches);
        Assert.Contains("public.t.status: default expected 'failed'", changedDefaultMismatches[0]);
        Assert.DoesNotContain("default expected 'pending'", changedDefaultMismatches[0]);
    }

    // Implements [CI-TEST-SHARED-CASE-MATRIX].
    [Theory]
    [MemberData(
        nameof(Verify_MultiRoleGrantInDesired_MatchedAgainstSingleRoleGrantsFromInspector_CasesData)
    )]
    public void Verify_MultiRoleGrantInDesired_MatchedAgainstSingleRoleGrantsFromInspector_Cases(
        string caseName,
        Func<SchemaDefinition> input0,
        Func<SchemaDefinition> input1,
        Func<SchemaDefinition, SchemaDefinition> input2,
        global::System.String value0
    )
    {
        Assert.False(condition: string.IsNullOrEmpty(value: caseName));
        ExerciseTypedCaseMatrix1(input0: input0, input1: input1, input2: input2, value0: value0);
    }

    internal static ImmutableArray<string> Verify(SchemaDefinition live, SchemaDefinition desired)
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
        return (Ok(actual: result)).Value;
    }

    // Implements [CI-TEST-SHARED-CASE-MATRIX].
    private static void ExerciseTypedCaseMatrix1(
        Func<SchemaDefinition> input0,
        Func<SchemaDefinition> input1,
        Func<SchemaDefinition, SchemaDefinition> input2,
        global::System.String value0
    )
    {
        var live = input0();
        var desired = input1();

        Assert.Empty(Verify(live: live, desired: desired));

        var missingAdminGrant = input2(live);

        var missingAdminMismatches = Verify(live: missingAdminGrant, desired: desired);

        Assert.Single(missingAdminMismatches);
        Assert.Contains(value0, missingAdminMismatches[0]);
        Assert.Contains("missing grant", missingAdminMismatches[0]);
    }
}
