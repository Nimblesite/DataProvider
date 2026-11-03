using SqlParser;
using SqlParser.Ast;
using SqlParser.Dialects;

namespace Nimblesite.DataProvider.Migration.Postgres;

/// <summary>
/// Validates declared PostgreSQL function ownership and search-path entries as
/// plain identifiers using the official SqlParserCS PostgreSQL parser — never
/// regex or hand parsing. Implements [RLS-PG-FUNCTION-SECURITY].
/// </summary>
internal static class PostgresFunctionSupportValidation
{
    /// <summary>
    /// Throws when the declared owner role or search-path schemas are not plain,
    /// unquoted PostgreSQL identifiers. A declared search path without an owner
    /// role is also rejected: search-path pinning is part of secure ownership.
    /// </summary>
    internal static void EnsureValid(PostgresFunctionDefinition function)
    {
        var ownerRole = function.OwnerRole?.Trim();
        if (string.IsNullOrWhiteSpace(ownerRole))
        {
            if (function.SearchPath.Count > 0)
            {
                throw new InvalidOperationException(
                    $"PostgreSQL function searchPath requires ownerRole: "
                        + $"{function.Schema}.{function.Name}"
                );
            }
            return;
        }

        EnsurePlainIdentifier(ownerRole, function, "ownerRole");
        if (function.SearchPath.Count == 0)
        {
            throw new InvalidOperationException(
                $"PostgreSQL function ownerRole requires a pinned search_path: "
                    + $"{function.Schema}.{function.Name}"
            );
        }

        foreach (var schema in function.SearchPath)
        {
            EnsurePlainIdentifier(schema, function, "searchPath");
        }
    }

    private static void EnsurePlainIdentifier(
        string value,
        PostgresFunctionDefinition function,
        string field
    )
    {
        if (string.IsNullOrWhiteSpace(value) || ParseAsIdentifier(value) != value)
        {
            throw new InvalidOperationException(
                $"PostgreSQL function {field} must be a plain identifier: "
                    + $"{function.Schema}.{function.Name} declares '{value}'"
            );
        }
    }

    /// <summary>
    /// Parses <c>SELECT &lt;value&gt;</c> with the PostgreSQL dialect and returns
    /// the identifier text when the value is exactly one unquoted identifier,
    /// otherwise <c>null</c>. Injections become extra statements, expressions or
    /// quoted identifiers and are rejected.
    /// </summary>
    private static string? ParseAsIdentifier(string value)
    {
        try
        {
            var statements = new Parser().ParseSql($"SELECT {value}", new PostgreSqlDialect());
            return statements.Count == 1
                && statements[0] is Statement.Select select
                && select.Query.Body is SetExpression.SelectExpression body
                && body.Select.Projection is { Count: 1 } projection
                && projection[0] is SelectItem.UnnamedExpression item
                && item.Expression is Expression.Identifier identifier
                && identifier.Ident.QuoteStyle is null
                ? identifier.Ident.Value
                : null;
        }
        catch (ParserException)
        {
            return null;
        }
    }
}
