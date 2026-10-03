using System.Text;

namespace Nimblesite.Lql.Core;

/// <summary>
/// Rewrites <c>table.column</c> qualifiers in rendered SQL expressions: a qualifier mapped
/// to an alias is replaced by it, one mapped to an empty string is removed. String literals
/// and quoted identifiers are copied verbatim. Implements [LQL-SUBQUERY-LAYOUT].
/// </summary>
internal static class QualifierRewriter
{
    internal static string Rewrite(
        string expression,
        IReadOnlyDictionary<string, string> qualifiers
    )
    {
        var sql = new StringBuilder(expression.Length);
        var i = 0;
        while (i < expression.Length)
        {
            i = expression[i] switch
            {
                '\'' or '"' => CopyQuoted(expression, i, sql),
                var c when IsQualifierStart(expression, i, c) => CopyIdentifier(
                    expression,
                    i,
                    qualifiers,
                    sql
                ),
                var c => Copy(c, i, sql),
            };
        }
        return sql.ToString();
    }

    // Only the first segment of a dotted path can be a table qualifier.
    private static bool IsQualifierStart(string expression, int i, char c) =>
        (char.IsLetter(c) || c == '_')
        && (
            i == 0 || !(char.IsLetterOrDigit(expression[i - 1]) || expression[i - 1] is '_' or '.')
        );

    private static int CopyIdentifier(
        string expression,
        int start,
        IReadOnlyDictionary<string, string> qualifiers,
        StringBuilder sql
    )
    {
        var end = start;
        while (
            end < expression.Length
            && (char.IsLetterOrDigit(expression[end]) || expression[end] == '_')
        )
        {
            end++;
        }
        var identifier = expression[start..end];
        var qualifies = end < expression.Length && expression[end] == '.';
        if (qualifies && qualifiers.TryGetValue(identifier, out var replacement))
        {
            sql.Append(replacement);
            return replacement.Length == 0 ? end + 1 : end;
        }
        sql.Append(identifier);
        return end;
    }

    private static int CopyQuoted(string expression, int start, StringBuilder sql)
    {
        var quote = expression[start];
        var end = start + 1;
        while (end < expression.Length && expression[end] != quote)
        {
            end++;
        }
        end = Math.Min(end + 1, expression.Length);
        sql.Append(expression, start, end - start);
        return end;
    }

    private static int Copy(char c, int i, StringBuilder sql)
    {
        sql.Append(c);
        return i + 1;
    }
}
