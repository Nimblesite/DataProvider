namespace Nimblesite.DataProvider.Migration.Core;

/// <summary>
/// Migration error with message and optional inner exception.
/// </summary>
/// <param name="Message">Error message</param>
/// <param name="InnerException">Optional inner exception</param>
public sealed record MigrationError(string Message, Exception? InnerException = null)
{
    /// <summary>
    /// Creates a migration error from a message.
    /// </summary>
    public static MigrationError FromMessage(string message) => new(message);

    /// <summary>
    /// Creates a migration error from an exception.
    /// </summary>
    public static MigrationError FromException(Exception ex) => new(ex.Message, ex);

    /// <inheritdoc />
    public override string ToString() =>
        InnerException is null ? Message : $"{Message}: {InnerException.Message}";

    // ─── RLS error codes — implements [RLS-ERRORS] ───────────────────

    /// <summary>
    /// <c>MIG-E-RLS-EMPTY-PREDICATE</c> — policy has SELECT/UPDATE/DELETE
    /// operations but <c>UsingLql</c> is missing.
    /// </summary>
    public static MigrationError RlsEmptyPredicate(string policyName) =>
        new($"MIG-E-RLS-EMPTY-PREDICATE: policy '{policyName}' is missing UsingLql");

    /// <summary>
    /// <c>MIG-E-RLS-EMPTY-CHECK</c> — policy has INSERT/UPDATE operations
    /// but <c>WithCheckLql</c> is missing.
    /// </summary>
    public static MigrationError RlsEmptyCheck(string policyName) =>
        new($"MIG-E-RLS-EMPTY-CHECK: policy '{policyName}' is missing WithCheckLql");

    /// <summary>
    /// <c>MIG-E-RLS-LQL-PARSE</c> — LQL predicate failed to parse.
    /// </summary>
    public static MigrationError RlsLqlParse(string policyName, string detail) =>
        new($"MIG-E-RLS-LQL-PARSE: policy '{policyName}': {detail}");

    /// <summary>
    /// <c>MIG-E-RLS-LQL-TRANSPILE</c> — LQL predicate transpilation failed.
    /// </summary>
    public static MigrationError RlsLqlTranspile(string policyName, string detail) =>
        new($"MIG-E-RLS-LQL-TRANSPILE: policy '{policyName}': {detail}");

    /// <summary>
    /// <c>MIG-E-RLS-MSSQL-UNSUPPORTED</c> — SQL Server RLS attempted before
    /// <c>Nimblesite.DataProvider.Migration.SqlServer</c> ships.
    /// </summary>
    public static MigrationError RlsMssqlUnsupported() =>
        new(
            "MIG-E-RLS-MSSQL-UNSUPPORTED: SQL Server RLS is not yet implemented. "
                + "Nimblesite.DataProvider.Migration.SqlServer package does not exist."
        );

    /// <summary>
    /// <c>MIG-E-RLS-RAW-SQL-UNSUPPORTED-ON-PLATFORM</c> — raw-SQL escape hatch
    /// (<c>UsingSql</c>/<c>WithCheckSql</c>) declared on a non-Postgres platform.
    /// Implements GitHub issue #36.
    /// </summary>
    public static MigrationError RlsRawSqlUnsupportedOnPlatform(
        string platform,
        string policyName
    ) =>
        new(
            $"MIG-E-RLS-RAW-SQL-UNSUPPORTED-ON-PLATFORM: policy '{policyName}' uses raw SQL "
                + $"predicate (UsingSql/WithCheckSql) which is Postgres-only; current platform: {platform}"
        );

    /// <summary>
    /// <c>MIG-E-RLS-FORCE-UNSUPPORTED-ON-PLATFORM</c> — <c>forced: true</c> declared on
    /// a non-Postgres platform. Implements GitHub issue #37.
    /// </summary>
    public static MigrationError RlsForceUnsupportedOnPlatform(string platform, string tableName) =>
        new(
            $"MIG-E-RLS-FORCE-UNSUPPORTED-ON-PLATFORM: table '{tableName}' has forced=true "
                + $"which is Postgres-only; current platform: {platform}"
        );
}
