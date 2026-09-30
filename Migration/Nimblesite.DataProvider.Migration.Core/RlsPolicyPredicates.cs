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

    internal static bool SameScope(RlsPolicyDefinition current, RlsPolicyDefinition desired) =>
        current.IsPermissive == desired.IsPermissive
        && EffectiveOperation(current.Operations) == EffectiveOperation(desired.Operations)
        && EffectiveRoles(current.Roles).SetEquals(EffectiveRoles(desired.Roles));

    internal static bool RequiresRecreate(
        RlsPolicyDefinition current,
        RlsPolicyDefinition desired
    ) =>
        !SameScope(current, desired)
        || HasPredicate(current.UsingSql, current.UsingLql)
            && !HasPredicate(desired.UsingSql, desired.UsingLql)
        || HasPredicate(current.WithCheckSql, current.WithCheckLql)
            && !HasPredicate(desired.WithCheckSql, desired.WithCheckLql);

    private static RlsOperation EffectiveOperation(IReadOnlyList<RlsOperation> operations) =>
        operations.Count == 0 || operations.Contains(RlsOperation.All)
            ? RlsOperation.All
            : operations[0];

    // Quoted PostgreSQL roles are case-sensitive. Empty and explicit PUBLIC
    // both denote every role; every other spelling is an exact identity.
    private static HashSet<string> EffectiveRoles(IReadOnlyList<string> roles) =>
        roles.Count == 0 || roles.Any(IsPublicRole)
            ? new HashSet<string>(["public"], StringComparer.Ordinal)
            : roles.ToHashSet(StringComparer.Ordinal);

    internal static bool IsPublicRole(string role) => role is "public" or "PUBLIC";

    private static bool HasPredicate(string? sql, string? lql) =>
        !string.IsNullOrWhiteSpace(sql) || !string.IsNullOrWhiteSpace(lql);

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
        if (current is Expression.InList currentIn && desired is Expression.AnyOp desiredAny)
        {
            return EquivalentInAndAny(currentIn, desiredAny);
        }
        if (current is Expression.AnyOp currentAny && desired is Expression.InList desiredIn)
        {
            return EquivalentInAndAny(desiredIn, currentAny);
        }
        if (
            current is Expression.Function currentFunction
            && desired is Expression.Function desiredFunction
        )
        {
            return EquivalentFunction(currentFunction, desiredFunction);
        }
        if (
            current is Expression.BinaryOp currentBinary
            && desired is Expression.BinaryOp desiredBinary
        )
        {
            return currentBinary.Op == desiredBinary.Op
                && Equivalent(currentBinary.Left, desiredBinary.Left)
                && (
                    Equivalent(currentBinary.Right, desiredBinary.Right)
                    || (
                        currentBinary.Op == BinaryOperator.Eq
                        && currentBinary.Left is Expression.Function
                        && SameBooleanLiteral(currentBinary.Right, desiredBinary.Right)
                    )
                );
        }
        return string.Equals(current.ToString(), desired.ToString(), StringComparison.Ordinal);
    }

    private static bool EquivalentInAndAny(Expression.InList list, Expression.AnyOp any)
    {
        if (
            list.Negated
            || any.CompareOp != BinaryOperator.Eq
            || any.Right is not Expression.Array array
            || list.List.Count != array.Arr.Element.Count
            || !Equivalent(list.Expression, any.Left)
        )
        {
            return false;
        }

        // PostgreSQL deparses a text IN-list as = ANY(ARRAY[...::text]).
        // Only ignore casts on these text literals; casts elsewhere can
        // change policy meaning and must remain visible as drift.
        return list
            .List.Zip(array.Arr.Element)
            .All(pair =>
                pair.Second
                    is Expression.Cast
                    {
                        DataType: DataType.Text,
                        Expression: Expression.LiteralValue { Value: Value.SingleQuotedString },
                    } cast
                    && Equivalent(pair.First, cast.Expression)
                || Equivalent(pair.First, pair.Second)
            );
    }

    private static bool EquivalentFunction(Expression.Function current, Expression.Function desired)
    {
        var currentName = current.Name.ToString();
        var desiredName = desired.Name.ToString();
        if (string.Equals(currentName, desiredName, StringComparison.Ordinal))
        {
            return string.Equals(current.ToString(), desired.ToString(), StringComparison.Ordinal);
        }
        return string.Equals(currentName, $"public.{desiredName}", StringComparison.Ordinal)
            && string.Equals(
                (current with { Name = desired.Name }).ToString(),
                desired.ToString(),
                StringComparison.Ordinal
            );
    }

    private static bool SameBooleanLiteral(Expression current, Expression desired)
    {
        var currentValue = BooleanLiteral(current);
        var desiredValue = BooleanLiteral(desired);
        return currentValue is not null && currentValue == desiredValue;
    }

    private static bool? BooleanLiteral(Expression expression)
    {
        if (expression is Expression.LiteralValue { Value: Value.Boolean value })
        {
            return value.Value;
        }
        if (
            expression is Expression.LiteralValue { Value: Value.SingleQuotedString quoted }
            && bool.TryParse(quoted.Value, out var parsed)
        )
        {
            return parsed;
        }
        return null;
    }

    private static string IdentifierKey(Ident identifier) =>
        identifier.QuoteStyle is null ? identifier.Value.ToLowerInvariant() : identifier.Value;
}
