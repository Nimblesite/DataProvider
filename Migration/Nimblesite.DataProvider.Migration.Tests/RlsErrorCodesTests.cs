namespace Nimblesite.DataProvider.Migration.Tests;

// Implements [RLS-ERRORS] tests from docs/specs/rls-spec.md.

/// <summary>
/// Smoke tests for RLS error code messages so external systems can rely on
/// the codes being present and machine-grep'able.
/// </summary>
public sealed class RlsErrorCodesTests
{
    [Fact]
    public void RlsMssqlUnsupported_HasCanonicalCode()
    {
        var err = MigrationError.RlsMssqlUnsupported();
        Assert.StartsWith("MIG-E-RLS-MSSQL-UNSUPPORTED", err.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RlsEmptyPredicate_IncludesPolicyName()
    {
        var err = MigrationError.RlsEmptyPredicate("documents_member");
        Assert.Contains("MIG-E-RLS-EMPTY-PREDICATE", err.Message, StringComparison.Ordinal);
        Assert.Contains("documents_member", err.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RlsEmptyCheck_IncludesPolicyName()
    {
        var err = MigrationError.RlsEmptyCheck("documents_member");
        Assert.Contains("MIG-E-RLS-EMPTY-CHECK", err.Message, StringComparison.Ordinal);
        Assert.Contains("documents_member", err.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RlsLqlParse_IncludesDetail()
    {
        var err = MigrationError.RlsLqlParse("p", "syntax at line 1");
        Assert.Contains("MIG-E-RLS-LQL-PARSE", err.Message, StringComparison.Ordinal);
        Assert.Contains("syntax at line 1", err.Message, StringComparison.Ordinal);
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
        Assert.Contains(
            "MIG-E-RLS-RAW-SQL-UNSUPPORTED-ON-PLATFORM",
            err.Message,
            StringComparison.Ordinal
        );
        Assert.Contains("Sqlite", err.Message, StringComparison.Ordinal);
        Assert.Contains("members_self", err.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RlsForceUnsupportedOnPlatform_NamesPlatformAndTable()
    {
        var err = MigrationError.RlsForceUnsupportedOnPlatform("Sqlite", "documents");
        Assert.Contains(
            "MIG-E-RLS-FORCE-UNSUPPORTED-ON-PLATFORM",
            err.Message,
            StringComparison.Ordinal
        );
        Assert.Contains("documents", err.Message, StringComparison.Ordinal);
    }
}
