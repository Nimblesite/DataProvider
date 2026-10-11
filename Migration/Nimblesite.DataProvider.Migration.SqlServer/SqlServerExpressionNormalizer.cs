using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace Nimblesite.DataProvider.Migration.SqlServer;

// Implements [MIG-SQLSERVER]. Canonicalize catalog parentheses using Microsoft's parser.
internal static class SqlServerExpressionNormalizer
{
    internal static string Normalize(string expression, ILogger? logger = null)
    {
        try
        {
            using var input = new StringReader(expression);
            var parsed = new TSql170Parser(initialQuotedIdentifiers: true).ParseBooleanExpression(
                input: input,
                errors: out var errors
            );
            return errors.Count == 0 && parsed is not null
                ? Boolean(expression: parsed)
                : expression;
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Unable to normalize SQL Server CHECK expression");
            return expression;
        }
    }

    private static string Boolean(BooleanExpression expression) =>
        expression switch
        {
            BooleanParenthesisExpression group => Boolean(expression: group.Expression),
            BooleanBinaryExpression binary =>
                $"({Boolean(expression: binary.FirstExpression)} {binary.BinaryExpressionType.ToString().ToUpperInvariant()} {Boolean(expression: binary.SecondExpression)})",
            BooleanNotExpression negation => $"NOT ({Boolean(expression: negation.Expression)})",
            BooleanComparisonExpression comparison
                when Operator(comparison.ComparisonType) is { } op =>
                $"({Scalar(expression: comparison.FirstExpression)} {op} {Scalar(expression: comparison.SecondExpression)})",
            _ => Script(fragment: expression),
        };

    private static string Scalar(ScalarExpression expression) =>
        expression switch
        {
            ParenthesisExpression group => Scalar(expression: group.Expression),
            ColumnReferenceExpression { MultiPartIdentifier: { } identifier } => string.Join(
                ".",
                identifier.Identifiers.Select(part =>
                    $"[{part.Value.Replace("]", "]]", StringComparison.Ordinal)}]"
                )
            ),
            _ => Script(fragment: expression),
        };

    private static string? Operator(BooleanComparisonType type) =>
        type switch
        {
            BooleanComparisonType.Equals => "=",
            BooleanComparisonType.GreaterThan => ">",
            BooleanComparisonType.LessThan => "<",
            BooleanComparisonType.GreaterThanOrEqualTo => ">=",
            BooleanComparisonType.LessThanOrEqualTo => "<=",
            BooleanComparisonType.NotEqualToBrackets
            or BooleanComparisonType.NotEqualToExclamation => "<>",
            BooleanComparisonType.NotLessThan => "!<",
            BooleanComparisonType.NotGreaterThan => "!>",
            _ => null,
        };

    private static string Script(TSqlFragment fragment)
    {
        new Sql170ScriptGenerator().GenerateScript(
            scriptFragment: fragment,
            script: out var script
        );
        return script;
    }
}
