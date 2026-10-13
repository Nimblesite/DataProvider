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
        end = global::Nimblesite.Lql.Core.QualifierRewriter.IdentifierEnd(
            source: expression,
            start: end
        );
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

    // Implements [LQL-SHARED-QUOTED-TOKEN].
    internal static int CopySqlQuoted(string source, int start, StringBuilder sql) =>
        source[start] == '\''
            ? CopyStringLiteral(source: source, sb: sql, i: start)
            : CopyQuoted(source: source, start: start, sql: sql, close: '"');

    private static int CopyQuoted(string expression, int start, StringBuilder sql) =>
        CopyQuoted(source: expression, start: start, sql: sql, close: expression[start]);

    internal static int CopyQuoted(string source, int start, StringBuilder sql, char close)
    {
        var end = start + 1;
        while (end < source.Length && source[end] != close)
        {
            end++;
        }
        end = Math.Min(end + 1, source.Length);
        sql.Append(source, start, end - start);
        return end;
    }

    private static int Copy(char c, int i, StringBuilder sql)
    {
        sql.Append(c);
        return i + 1;
    }

    // Implements [LQL-SHARED-LITERAL-COPY].
    internal static int CopyStringLiteral(string source, StringBuilder sb, int i)
    {
        var end = StringLiteralEnd(source: source, index: i + 1);
        sb.Append(source, i, end - i);
        return end;
    }

    private static int StringLiteralEnd(string source, int index)
    {
        while (index < source.Length)
        {
            if (source[index] == '\'')
            {
                if (index + 1 == source.Length || source[index + 1] != '\'')
                {
                    return index + 1;
                }
                index++;
            }
            index++;
        }
        return index;
    }

    // Implements [LQL-SHARED-IDENTIFIER-END].
    internal static int IdentifierEnd(string source, int start)
    {
        var end = start;
        while (end < source.Length && (char.IsLetterOrDigit(source[end]) || source[end] == '_'))
            end++;
        return end;
    }
}
