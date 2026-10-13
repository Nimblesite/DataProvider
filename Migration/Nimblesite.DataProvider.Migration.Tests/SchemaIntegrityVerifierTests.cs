using System.Collections.Immutable;
using static global::Tests.Shared.ResultAssertions;

// Implements [MIG-TEST-SCHEMA-FIXTURES].

namespace Nimblesite.DataProvider.Migration.Tests;

public sealed partial class SchemaIntegrityVerifierTests
{
    [Fact]
    public void Verify_EquivalentDefaultSchemasAndSqliteIntegerWidths_ReturnsNoMismatches()
    {
        var mismatches = VerifyConsolidatedPhase3(
            input0: Fixture_Verify_EquivalentDefaultSchemasAndSqliteIntegerWidths_ReturnsNoMismatches_1,
            input1: Fixture_Verify_EquivalentDefaultSchemasAndSqliteIntegerWidths_ReturnsNoMismatches_2
        );

        Assert.Empty(mismatches);
    }

    [Fact]
    public void Verify_WhenColumnsAndConstraintsDrift_ReturnsDetailedMismatches()
    {
        var mismatches = VerifyConsolidatedPhase3(
            input0: Fixture_Verify_WhenColumnsAndConstraintsDrift_ReturnsDetailedMismatches_1,
            input1: Fixture_Verify_WhenColumnsAndConstraintsDrift_ReturnsDetailedMismatches_2
        );

        Assert.Contains("public.missing_table: missing table", mismatches);
        Assert.Contains(
            $"public.accounts.id: type expected {PortableTypes.Uuid} but found {PortableTypes.Int}",
            mismatches
        );
        AssertIntegrityMismatchMessages(
            input0: mismatches,
            expected: Fixture_WhenColumnsAndConstraintsDrift_ReturnsDetailedMismatches_2()
        );
        AssertIntegrityMismatchMessages(
            input0: mismatches,
            expected: Fixture_WhenColumnsAndConstraintsDrift_ReturnsDetailedMismatches_3()
        );
        AssertIntegrityMismatchMessages(
            input0: mismatches,
            expected: Fixture_WhenColumnsAndConstraintsDrift_ReturnsDetailedMismatches_4()
        );
        Assert.Contains("public.accounts.status: missing check constraint", mismatches);
    }

    [Fact]
    public void Verify_WhenPrimaryKeyIsMissing_ReturnsMissingPrimaryKey()
    {
        var mismatches = VerifyConsolidatedPhase3(
            input0: Data_Verify_WhenPrimaryKeyIsMissing_ReturnsMissingPrimaryKey_1,
            input1: Fixture_Verify_WhenPrimaryKeyIsMissing_ReturnsMissingPrimaryKey_1
        );

        Assert.Contains("public.accounts: missing primary key", mismatches);
    }

    [Fact]
    public void Verify_WhenRlsAndSupportObjectsDrift_ReturnsDetailedMismatches()
    {
        var mismatches = VerifyConsolidatedPhase3(
            input0: Fixture_Verify_WhenRlsAndSupportObjectsDrift_ReturnsDetailedMismatches_1,
            input1: Fixture_Verify_WhenRlsAndSupportObjectsDrift_ReturnsDetailedMismatches_2
        );

        AssertIntegrityMismatchMessages(
            input0: mismatches,
            expected: Fixture_WhenRlsAndSupportObjectsDrift_ReturnsDetailedMismatches_2()
        );
        Assert.Contains("grant Table documents: missing grant", mismatches);
    }

    [Fact]
    public void Verify_WhenRlsAndSupportObjectsAreExcluded_IgnoresThoseMismatches()
    {
        var live = Dataset_Verify_WhenRlsAndSupportObjectsAreExcluded_IgnoresThoseMismatches_1();
        var desired = Fixture_Verify_WhenRlsAndSupportObjectsAreExcluded_IgnoresThoseMismatches_1();

        var mismatches = Verify(
            live: live,
            desired: desired,
            includeSupportObjects: false,
            includeRls: false
        );

        Assert.Empty(mismatches);
    }

    [Fact]
    public void Verify_WhenSchemaCannotBeRead_ReturnsError()
    {
        global::Tests.Shared.ResultAssertions.Error(
            actual: SchemaIntegrityVerifier.Verify(
                live: Schema.Define("live").Build(),
                desired: new SchemaDefinition { Tables = null! },
                logger: NullLogger.Instance
            )
        );
    }

    private static ImmutableArray<string> Verify(
        SchemaDefinition live,
        SchemaDefinition desired,
        bool includeSupportObjects = true,
        bool includeRls = true
    )
    {
        var result = SchemaIntegrityVerifier.Verify(
            live: live,
            desired: desired,
            includeSupportObjects: includeSupportObjects,
            includeRls: includeRls,
            logger: NullLogger.Instance
        );

        Assert.True(
            result is SchemaIntegrityOk,
            "Expected schema integrity verification to succeed."
        );
        return (Ok(actual: result)).Value;
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private static void AssertIntegrityMismatchMessages(
        global::System.Collections.Immutable.ImmutableArray<global::System.String> input0,
        IReadOnlyList<string> expected
    )
    {
        foreach (var message in expected)
        {
            Assert.Contains(message, input0);
        }
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    internal static ImmutableArray<string> VerifyConsolidatedPhase3(
        Func<SchemaDefinition> input0,
        Func<SchemaDefinition> input1
    )
    {
        var live = input0();
        var desired = input1();

        var mismatches = Verify(live: live, desired: desired);

        return mismatches;
    }
}
