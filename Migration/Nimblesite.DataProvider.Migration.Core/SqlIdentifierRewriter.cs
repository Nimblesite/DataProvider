using System.Text;

namespace Nimblesite.DataProvider.Migration.Core;

/// <summary>
/// Shared bare-identifier tokenizer used by RLS predicate quoting and
/// PostgreSQL column-reference quoting.
/// </summary>
internal static class SqlIdentifierRewriter
{
    /// <summary>
    /// Copies single-quoted strings and already-quoted spans verbatim through
    /// <paramref name="copySpecial" /> and defers the quote/no-quote decision for
    /// each bare word to <paramref name="shouldQuote" />.
    /// </summary>
    internal static string Rewrite(
        string source,
        char open,
        char close,
        Func<int, StringBuilder, int> copySpecial,
        Func<string, int, bool> shouldQuote
    )
    {
        var sb = new StringBuilder(source.Length + 16);
        var i = 0;
        while (i < source.Length)
        {
            var c = source[i];
            if (c == '\'' || c == open)
            {
                i = copySpecial(i, sb);
                continue;
            }
            if (char.IsLetter(c) || c == '_')
            {
                var start = i;
                i++;
                i = global::Nimblesite.Lql.Core.QualifierRewriter.IdentifierEnd(
                    source: source,
                    start: i
                );
                var word = source[start..i];
                if (shouldQuote(word, start))
                {
                    sb.Append(open).Append(word).Append(close);
                }
                else
                {
                    sb.Append(word);
                }
                continue;
            }
            sb.Append(c);
            i++;
        }
        return sb.ToString();
    }
}
