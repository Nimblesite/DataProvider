using Nimblesite.Sql.Model;

namespace Nimblesite.Lql.Core;

/// <summary>
/// Validates an <see cref="LqlStatement"/> before dialect conversion.
/// Shared by all platform-specific To*Sql extension methods to avoid duplicating
/// the parse-error and missing-AST guards.
/// </summary>
public static class LqlStatementValidator
{
    /// <summary>
    /// Returns the reason the statement cannot be converted, or <c>null</c> when it is valid.
    /// </summary>
    /// <param name="statement">The statement to validate.</param>
    /// <returns>
    /// The parse error when present, a "no AST node" error when the statement has no AST node,
    /// or <c>null</c> when the statement has a usable AST node.
    /// </returns>
    public static SqlError? Validate(LqlStatement statement) =>
        statement.ParseError is SqlError parseError ? parseError
        : statement.AstNode is null ? new SqlError("No AST node found in statement")
        : null;
}
