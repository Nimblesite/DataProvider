namespace Nimblesite.DataProvider.Migration.Tests;

// Implements [RLS-ERRORS] tests from docs/specs/rls-spec.md.

/// <summary>
/// Smoke tests for RLS error code messages so external systems can rely on
/// the codes being present and machine-grep'able.
/// </summary>
public sealed partial class RlsErrorCodesTests
{
    [Fact]
    public void RlsMssqlUnsupported_HasCanonicalCode()
    {
        var err = MigrationError.RlsMssqlUnsupported();
        Assert.StartsWith("MIG-E-RLS-MSSQL-UNSUPPORTED", err.Message, StringComparison.Ordinal);
    }

    // Implements [CI-TEST-SHARED-CASE-MATRIX].
    [Theory]
    [MemberData(nameof(RlsEmptyPredicate_IncludesPolicyName_ContextCasesData))]
    public void RlsEmptyPredicate_IncludesPolicyName_ContextCases(
        string caseName,
        global::System.Func<
            global::System.String,
            global::Nimblesite.DataProvider.Migration.Core.MigrationError
        > value0,
        string value2
    )
    {
        Assert.False(condition: string.IsNullOrEmpty(value: caseName));
        ExerciseOperationCaseMatrix3(value0: value0, value2: value2);
    }

    // Implements [CI-TEST-SHARED-CASE-MATRIX].
    [Theory]
    [MemberData(nameof(RlsLqlParse_IncludesDetail_ContextCasesData))]
    public void RlsLqlParse_IncludesDetail_ContextCases(
        string caseName,
        global::System.Func<
            global::System.String,
            global::System.String,
            global::Nimblesite.DataProvider.Migration.Core.MigrationError
        > value0,
        string value1,
        string value2,
        string value3,
        string value4
    )
    {
        Assert.False(condition: string.IsNullOrEmpty(value: caseName));
        ExerciseOperationCaseMatrix4(
            value0: value0,
            value1: value1,
            value2: value2,
            value3: value3,
            value4: value4
        );
    }

    [Fact]
    public void RlsLqlTranspile_IncludesDetail()
    {
        var err = MigrationError.RlsLqlTranspile("p", "unknown function");
        Assert.Contains("MIG-E-RLS-LQL-TRANSPILE", err.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RlsRawSqlUnsupportedOnPlatform_NamesPlatformAndPolicy()
    {
        var err = MigrationError.RlsRawSqlUnsupportedOnPlatform("Sqlite", "members_self");
        global::Nimblesite.DataProvider.Migration.Tests.RlsPredicateTranspilerTests.VerifyCrossClassChecks3(
            input0: err,
            value0: "MIG-E-RLS-RAW-SQL-UNSUPPORTED-ON-PLATFORM",
            value2: "Sqlite"
        );
        Assert.Contains("members_self", err.Message, StringComparison.Ordinal);
    }

    // Implements [CI-TEST-SHARED-CASE-MATRIX].
    private static void ExerciseOperationCaseMatrix3(
        global::System.Func<
            global::System.String,
            global::Nimblesite.DataProvider.Migration.Core.MigrationError
        > value0,
        string value2
    )
    {
        var err = value0(arg: "documents_member");
        global::Nimblesite.DataProvider.Migration.Tests.RlsPredicateTranspilerTests.VerifyCrossClassChecks3(
            input0: err,
            value0: value2,
            value2: "documents_member"
        );
    }

    // Implements [CI-TEST-SHARED-CASE-MATRIX].
    private static void ExerciseOperationCaseMatrix4(
        global::System.Func<
            global::System.String,
            global::System.String,
            global::Nimblesite.DataProvider.Migration.Core.MigrationError
        > value0,
        string value1,
        string value2,
        string value3,
        string value4
    )
    {
        var err = value0(arg1: value1, arg2: value2);
        global::Nimblesite.DataProvider.Migration.Tests.RlsPredicateTranspilerTests.VerifyCrossClassChecks3(
            input0: err,
            value0: value3,
            value2: value4
        );
    }
}
