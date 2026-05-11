using SqlParser;
using SqlParser.Ast;
using SqlParser.Dialects;

namespace Nimblesite.DataProvider.Migration.Postgres;

/// <summary>
/// Implements [MIG-CHECK-CONSTRAINT-EXPRESSION-DRIFT] (#57): PostgreSQL
/// reformats check-constraint predicates when reading them back from
/// <c>pg_get_expr</c> — it wraps everything in parens, replaces
/// <c>col IN ('a', 'b')</c> with <c>(col = ANY (ARRAY['a'::text, 'b'::text]))</c>,
/// and adds <c>::text</c> casts around plain identifiers. To make drift
/// detection stable across reapply, the inspector reverses those rewrites
/// so the live expression matches the YAML literal form before storage in
/// <see cref="CheckConstraintDefinition.Expression"/>.
/// </summary>
internal static class PostgresCheckExpressionNormalizer
{
    /// <summary>
    /// Normalise a check-constraint expression read from <c>pg_get_expr</c>
    /// into the form the user is likely to have written in YAML. Strips
    /// added <c>::text</c> casts, unwraps superfluous outer parens, and
    /// rewrites <c>(col = ANY (ARRAY[lit, ...]))</c> back to <c>col IN (...)</c>.
    /// Returns the input unchanged when parsing fails.
    /// </summary>
    public static string Normalize(string expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            return expression;
        }

        var parsed = TryParseExpression(expression.Trim());
        if (parsed is null)
        {
            return expression;
        }

        return RenderCanonical(parsed);
    }

    private static Expression? TryParseExpression(string expression)
    {
        try
        {
            var sql = $"SELECT 1 WHERE {expression}";
            var statements = new Parser().ParseSql(sql, new PostgreSqlDialect());
            if (statements.Count == 0 || statements[0] is not Statement.Select select)
            {
                return null;
            }
            if (select.Query.Body is not SetExpression.SelectExpression body)
            {
                return null;
            }
            return body.Select.Selection;
        }
        catch (ParserException)
        {
            return null;
        }
    }

    private static string RenderCanonical(Expression expression)
    {
        var simplified = Simplify(expression);
        var rewritten = TryRewriteAnyArrayToIn(simplified);
        if (rewritten is not null)
        {
            return rewritten;
        }
        return Render(simplified);
    }

    // Strip noise added by pg_get_expr: outer Nested wrappers, redundant
    // (x)::text casts. These don't change semantics but break naive string
    // comparison against YAML.
    private static Expression Simplify(Expression expression)
    {
        if (expression is Expression.Nested n)
        {
            return Simplify(n.Expression);
        }
        if (expression is Expression.Cast cast && IsRedundantCast(cast))
        {
            return Simplify(cast.Expression);
        }
        if (expression is Expression.BinaryOp bin)
        {
            return new Expression.BinaryOp(Simplify(bin.Left), bin.Op, Simplify(bin.Right));
        }
        if (expression is Expression.AnyOp any)
        {
            return new Expression.AnyOp(
                Simplify(any.Left),
                any.CompareOp,
                Simplify(any.Right),
                any.IsSome
            );
        }
        if (expression is Expression.Array arr)
        {
            var simpler = new Sequence<Expression>(arr.Arr.Element.Select(Simplify));
            return new Expression.Array(new ArrayExpression(simpler, arr.Arr.Named));
        }
        return expression;
    }

    private static bool IsRedundantCast(Expression.Cast cast)
    {
        // Casts to common Postgres scalar types that pg_get_expr adds around
        // every identifier reference. These are semantically transparent in
        // the context of a CHECK predicate.
        if (cast.DataType is not DataType.Text and not DataType.Varchar and not DataType.Custom)
        {
            return false;
        }
        return true;
    }

    private static string? TryRewriteAnyArrayToIn(Expression expression)
    {
        if (expression is not Expression.AnyOp any || any.Right is not Expression.Array arr)
        {
            return null;
        }
        var literals = new List<string>();
        foreach (var element in arr.Arr.Element)
        {
            var literal = ExtractStringLiteral(element);
            if (literal is null)
            {
                return null;
            }
            literals.Add(literal);
        }
        return $"{Render(any.Left)} IN ({string.Join(", ", literals)})";
    }

    private static string? ExtractStringLiteral(Expression element)
    {
        var unwrapped = element;
        if (unwrapped is Expression.Cast cast)
        {
            unwrapped = cast.Expression;
        }
        if (unwrapped is not Expression.LiteralValue lit)
        {
            return null;
        }
        if (lit.Value is Value.SingleQuotedString s)
        {
            return $"'{s.Value.Replace("'", "''", StringComparison.Ordinal)}'";
        }
        if (lit.Value is Value.Number n)
        {
            return n.Value;
        }
        return null;
    }

    private static string Render(Expression expression)
    {
        if (expression is Expression.Identifier id)
        {
            return id.Ident.Value;
        }
        if (expression is Expression.CompoundIdentifier compound)
        {
            return string.Join(".", compound.Idents.Select(i => i.Value));
        }
        if (expression is Expression.LiteralValue lit)
        {
            if (lit.Value is Value.SingleQuotedString s)
            {
                return $"'{s.Value.Replace("'", "''", StringComparison.Ordinal)}'";
            }
            if (lit.Value is Value.Number n)
            {
                return n.Value;
            }
        }
        if (expression is Expression.BinaryOp bin)
        {
            return $"{Render(bin.Left)} {RenderOp(bin.Op)} {Render(bin.Right)}";
        }
        if (expression is Expression.Nested nested)
        {
            return $"({Render(nested.Expression)})";
        }
        if (expression is Expression.Cast cast)
        {
            return Render(cast.Expression);
        }
        // Fallback: stringify via type name only; lossy but deterministic.
        return expression.GetType().Name;
    }

    private static string RenderOp(BinaryOperator op)
    {
        if (op == BinaryOperator.PGRegexMatch)
        {
            return "~";
        }
        if (op == BinaryOperator.PGRegexNotMatch)
        {
            return "!~";
        }
        if (op == BinaryOperator.PGRegexIMatch)
        {
            return "~*";
        }
        if (op == BinaryOperator.PGRegexNotIMatch)
        {
            return "!~*";
        }
        if (op == BinaryOperator.Eq)
        {
            return "=";
        }
        if (op == BinaryOperator.NotEq)
        {
            return "<>";
        }
        if (op == BinaryOperator.Lt)
        {
            return "<";
        }
        if (op == BinaryOperator.LtEq)
        {
            return "<=";
        }
        if (op == BinaryOperator.Gt)
        {
            return ">";
        }
        if (op == BinaryOperator.GtEq)
        {
            return ">=";
        }
        if (op == BinaryOperator.And)
        {
            return "AND";
        }
        if (op == BinaryOperator.Or)
        {
            return "OR";
        }
        return op.ToString();
    }
}
