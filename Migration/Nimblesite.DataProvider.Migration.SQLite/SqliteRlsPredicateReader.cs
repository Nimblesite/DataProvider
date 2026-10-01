using SqlParser;
using SqlParser.Ast;
using SqlParser.Dialects;

namespace Nimblesite.DataProvider.Migration.SQLite;

// Implements [RLS-DIFF] SQLite predicate extraction. The stored secure-view
// WHERE clause and trigger-body predicate are read back with the official
// SQL parser (SqlParserCS, SQLiteDialect) so an existing policy's USING /
// WITH CHECK predicate can be compared against the desired one. The trigger
// body is located with a quote-aware keyword scan because SqlParserCS 0.6.5
// cannot parse CREATE TRIGGER statements as a whole.

internal static class SqliteRlsPredicateReader
{
    /// <summary>
    /// Read the WHERE predicate of the table's <c>{tableName}_secure</c> view.
    /// Returns null when the view does not exist or its predicate could not be
    /// extracted; <paramref name="exists"/> distinguishes the two cases.
    /// </summary>
    public static (bool Exists, string? Predicate) ReadSecureView(
        SqliteConnection connection,
        string tableName
    )
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT sql FROM sqlite_master WHERE type = 'view' AND name = @name";
        command.Parameters.AddWithValue("@name", $"{tableName}_secure");
        var sql = command.ExecuteScalar() as string;
        return sql is null ? (false, null) : (true, ViewPredicate(sql));
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
        selection switch
        {
            Expression.UnaryOp { Op: UnaryOperator.Not } not => Unwrap(not.Expression),
            _ => null,
        };

    private static string? Unwrap(Expression expression)
    {
        var inner = expression is Expression.Nested nested ? nested.Expression : expression;
        return inner is null ? null : StripRowAlias(inner).ToSql();
    }

    private static Expression StripRowAlias(Expression expression) =>
        expression switch
        {
            Expression.CompoundIdentifier c when c.Idents.Count > 1 && IsRowAlias(c.Idents[0]) =>
                c with
                {
                    Idents = [.. c.Idents.Skip(1)],
                },
            Expression.BinaryOp binary => binary with
            {
                Left = StripRowAlias(binary.Left),
                Right = StripRowAlias(binary.Right),
            },
            Expression.UnaryOp unary => unary with { Expression = StripRowAlias(unary.Expression) },
            Expression.Nested nested => StripRowAlias(nested.Expression),
            Expression.IsNull check => check with { Expression = StripRowAlias(check.Expression) },
            Expression.IsNotNull check => check with
            {
                Expression = StripRowAlias(check.Expression),
            },
            _ => expression,
        };

    // Only the generated unquoted NEW./OLD. aliases are removed; a column
    // literally named "new" is always bracket-quoted and therefore kept.
    private static bool IsRowAlias(Ident ident) =>
        ident.QuoteStyle is null && ident.Value is "NEW" or "OLD";

    /// <summary>
    /// Return the trigger body between the first top-level BEGIN and the last
    /// top-level END. The scan skips string literals and bracket identifiers
    /// so policy names or columns containing BEGIN/END cannot confuse it.
    /// </summary>
    private static string? SliceTriggerBody(string triggerSql)
    {
        var markers = KeywordMarkers(triggerSql);
        return markers is { } range
            ? triggerSql[(range.Begin + "BEGIN".Length)..range.End].Trim().TrimEnd(';')
            : null;
    }

    private static (int Begin, int End)? KeywordMarkers(string sql)
    {
        int? begin = null;
        var end = -1;
        for (var i = 0; i < sql.Length; i++)
        {
            if (AtStringStart(sql, i))
            {
                i = EndOfString(sql, i);
            }
            else if (AtBracketStart(sql, i))
            {
                i = EndOfBracket(sql, i);
            }
            else if (IsKeyword(sql, i, "BEGIN"))
            {
                begin ??= i;
            }
            else if (IsKeyword(sql, i, "END"))
            {
                end = i;
            }
        }
        return begin is { } start && end > start ? (start, end) : null;
    }

    private static bool AtStringStart(string sql, int index) => sql[index] == '\'';

    private static bool AtBracketStart(string sql, int index) => sql[index] == '[';

    private static int EndOfString(string sql, int start)
    {
        for (var i = start + 1; i < sql.Length; i++)
        {
            if (sql[i] == '\'' && (i + 1 >= sql.Length || sql[i + 1] != '\''))
            {
                return i;
            }
            i += sql[i] == '\'' ? 1 : 0;
        }
        return sql.Length - 1;
    }

    private static int EndOfBracket(string sql, int start)
    {
        for (var i = start + 1; i < sql.Length; i++)
        {
            if (sql[i] == ']' && (i + 1 >= sql.Length || sql[i + 1] != ']'))
            {
                return i;
            }
            i += sql[i] == ']' ? 1 : 0;
        }
        return sql.Length - 1;
    }

    private static bool IsKeyword(string sql, int index, string keyword) =>
        index + keyword.Length <= sql.Length
        && sql.StartsWith(keyword, index, StringComparison.OrdinalIgnoreCase)
        && !IsIdentChar(sql, index - 1)
        && !IsIdentChar(sql, index + keyword.Length);

    private static bool IsIdentChar(string sql, int index) =>
        index >= 0 && index < sql.Length && (char.IsLetterOrDigit(sql[index]) || sql[index] == '_');
}
