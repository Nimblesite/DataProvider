using SqlParser;
using SqlParser.Ast;
using SqlParser.Dialects;
using PredicateOk = Outcome.Result<
    string,
    Nimblesite.DataProvider.Migration.Core.MigrationError
>.Ok<string, Nimblesite.DataProvider.Migration.Core.MigrationError>;

namespace Nimblesite.DataProvider.Migration.Core;

// Implements [RLS-DIFF]: compare parsed PostgreSQL predicates so harmless
// catalog formatting does not hide changes or cause endless reapplication.
internal static class RlsPolicyPredicates
{
    internal static bool SameUsing(RlsPolicyDefinition current, RlsPolicyDefinition desired) =>
        Same(current.UsingSql, current.UsingLql, desired.UsingSql, desired.UsingLql, desired.Name);

    internal static bool SameWithCheck(RlsPolicyDefinition current, RlsPolicyDefinition desired) =>
        Same(
            current.WithCheckSql,
            current.WithCheckLql,
            desired.WithCheckSql,
            desired.WithCheckLql,
            desired.Name
        );

    private static bool Same(
        string? currentSql,
        string? currentLql,
        string? desiredSql,
        string? desiredLql,
        string policyName
    )
    {
        var current = Resolve(currentSql, currentLql, policyName);
        var desired = Resolve(desiredSql, desiredLql, policyName);
        if (!current.Success || !desired.Success)
        {
            return false;
        }
        var currentExpression = Parse(current.Sql);
        var desiredExpression = Parse(desired.Sql);
        return currentExpression is not null && desiredExpression is not null
            ? Equivalent(currentExpression, desiredExpression)
            : string.Equals(current.Sql?.Trim(), desired.Sql?.Trim(), StringComparison.Ordinal);
    }

    private static (bool Success, string? Sql) Resolve(string? sql, string? lql, string name)
    {
        if (!string.IsNullOrWhiteSpace(sql))
        {
            return (true, sql);
        }
        if (string.IsNullOrWhiteSpace(lql))
        {
            return (true, null);
        }
        var translated = RlsPredicateTranspiler.Translate(lql, RlsPlatform.Postgres, name);
        return translated is PredicateOk ok ? (true, ok.Value) : (false, null);
    }

    private static Expression? Parse(string? sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
        {
            return null;
        }
        try
        {
            var statements = new Parser().ParseSql(
                $"SELECT 1 WHERE {sql}",
                new PostgreSqlDialect()
            );
            if (
                statements.Count == 1
                && statements[0] is Statement.Select select
                && select.Query.Body is SetExpression.SelectExpression body
                && body.Select.Selection is Expression expression
            )
            {
                return expression;
            }
        }
        catch (ParserException)
        {
            // Keep unparseable SQL distinct unless its text is exactly equal.
        }
        return null;
    }

    private static bool Equivalent(Expression current, Expression desired)
    {
        if (current is Expression.Nested currentNested)
        {
            return Equivalent(currentNested.Expression, desired);
        }
        if (desired is Expression.Nested desiredNested)
        {
            return Equivalent(current, desiredNested.Expression);
        }
        if (
            current is Expression.Identifier currentId
            && desired is Expression.Identifier desiredId
        )
        {
            return string.Equals(
                IdentifierKey(currentId.Ident),
                IdentifierKey(desiredId.Ident),
                StringComparison.Ordinal
            );
        }
        if (
            current is Expression.BinaryOp currentBinary
            && desired is Expression.BinaryOp desiredBinary
        )
        {
            return currentBinary.Op == desiredBinary.Op
                && Equivalent(currentBinary.Left, desiredBinary.Left)
                && Equivalent(currentBinary.Right, desiredBinary.Right);
        }
        return string.Equals(current.ToString(), desired.ToString(), StringComparison.Ordinal);
    }

    private static string IdentifierKey(Ident identifier) =>
        identifier.QuoteStyle is null ? identifier.Value.ToLowerInvariant() : identifier.Value;
}
