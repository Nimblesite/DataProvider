using System.Collections.Immutable;
using SqlParser;
using SqlParser.Ast;
using SqlParser.Dialects;
using SqlParser.Tokens;

namespace Nimblesite.DataProvider.Migration.SQLite;

// Implements [RLS-DIFF] SQLite predicate extraction. The stored secure-view
// WHERE clause and trigger-body predicate are read back with the official
// SQL parser (SqlParserCS, SQLiteDialect) so an existing policy's USING /
// WITH CHECK predicate can be compared against the desired one. The trigger
// body is located with the same parser's tokenizer because SqlParserCS 0.6.5
// cannot parse CREATE TRIGGER statements as a whole.

internal static class SqliteRlsPredicateReader
{
    /// <summary>
    /// Read the WHERE predicate of the table's <c>{tableName}_secure</c> view.
    /// Returns null when the view does not exist or its predicate could not be
    /// extracted; the returned Exists value distinguishes the two cases.
    /// </summary>
    public static (bool Exists, string? Predicate, string? PolicyName) ReadSecureView(
        SqliteConnection connection,
        string tableName
    )
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT sql FROM sqlite_master WHERE type = 'view' AND name = @name";
        command.Parameters.AddWithValue("@name", $"{tableName}_secure");
        return command.ExecuteScalar() is string sql
            ? (true, ViewPredicate(sql), SqliteRlsViewMetadata.ReadPolicyName(sql: sql))
            : (false, null, null);
    }

    /// <summary>
    /// Extract the RAISE-guard predicate from a stored RLS trigger, with the
    /// NEW./OLD. row alias stripped so the text matches the plain predicate
    /// the policy declares. Null when the trigger is not in the generated
    /// shape or its body cannot be parsed.
    /// </summary>
    public static string? TriggerPredicate(string triggerSql) =>
        SliceTriggerBody(triggerSql) is not { } body ? null : GuardPredicate(body);

    private static string? ViewPredicate(string viewSql)
    {
        try
        {
            var statements = new Parser().ParseSql(viewSql, new SQLiteDialect());
            return
                statements.Count == 1
                && statements[0] is Statement.CreateView view
                && view.Query.Query.Body is SetExpression.SelectExpression body
                ? body.Select.Selection?.ToSql()
                : null;
        }
        catch (ParserException)
        {
            return null;
        }
    }

    private static string? GuardPredicate(string bodySql)
    {
        try
        {
            var statements = new Parser().ParseSql(bodySql, new SQLiteDialect());
            return
                statements.Count == 1
                && statements[0] is Statement.Select select
                && select.Query.Body is SetExpression.SelectExpression body
                ? GuardedPredicate(body.Select.Selection)
                : null;
        }
        catch (ParserException)
        {
            return null;
        }
    }

    // The generated body is `SELECT RAISE(ABORT, ...) WHERE NOT (<predicate>)`.
    private static string? GuardedPredicate(Expression? selection) =>
        selection is Expression.UnaryOp { Op: UnaryOperator.Not } not
            ? Unwrap(not.Expression)
            : null;

    private static string? Unwrap(Expression expression)
    {
        var inner = expression is Expression.Nested nested ? nested.Expression : expression;
        return inner is null ? null : StripRowAlias(inner).ToSql();
    }

    private static Expression StripRowAlias(Expression expression) =>
        expression is Expression.CompoundIdentifier c
        && c.Idents.Count > 1
        && IsRowAlias(c.Idents[0])
            ? c with
            {
                Idents = [.. c.Idents.Skip(1)],
            }
        : expression is Expression.BinaryOp binary
            ? binary with
            {
                Left = StripRowAlias(binary.Left),
                Right = StripRowAlias(binary.Right),
            }
        : expression is Expression.UnaryOp unary
            ? unary with
            {
                Expression = StripRowAlias(unary.Expression),
            }
        : expression is Expression.Nested nested ? StripRowAlias(nested.Expression)
        : StripNullChecks(expression);

    private static Expression StripNullChecks(Expression expression) =>
        expression is Expression.IsNull check
            ? check with
            {
                Expression = StripRowAlias(check.Expression),
            }
        : expression is Expression.IsNotNull nonNull
            ? nonNull with
            {
                Expression = StripRowAlias(nonNull.Expression),
            }
        : expression;

    // Only the generated unquoted NEW./OLD. aliases are removed; a column
    // literally named "new" is always bracket-quoted and therefore kept.
    private static bool IsRowAlias(Ident ident) =>
        ident.QuoteStyle is null && ident.Value is "NEW" or "OLD";

    /// <summary>
    /// Return the trigger body using SQL keyword tokens and their source locations.
    /// </summary>
    private static string? SliceTriggerBody(string triggerSql)
    {
        try
        {
            var words = new Tokenizer(unescape: false)
                .Tokenize(sql: triggerSql, dialect: new SQLiteDialect())
                .OfType<Word>()
                .ToImmutableArray();
            return SliceBody(sql: triggerSql, words: words);
        }
        catch (TokenizeException)
        {
            return null;
        }
    }

    private static string? SliceBody(string sql, ImmutableArray<Word> words)
    {
        var begin = words.FirstOrDefault(word => word.Keyword == Keyword.BEGIN);
        var end = words.LastOrDefault(word => word.Keyword == Keyword.END);
        return begin is { } first && end is { } last
            ? sql[
                (SourceOffset(sql: sql, location: first.Location) + 5)..SourceOffset(
                    sql: sql,
                    location: last.Location
                )
            ]
                .Trim()
                .TrimEnd(';')
            : null;
    }

    private static int SourceOffset(string sql, Location location) =>
        sql.Split('\n').Take(Convert.ToInt32(location.Line) - 1).Sum(line => line.Length + 1)
        + Convert.ToInt32(location.Column)
        - 1;
}
