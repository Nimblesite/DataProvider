using System.Text;

namespace Nimblesite.DataProvider.Migration.Core;

/// <summary>
/// Splits the parenthesised body of a platform index definition into individual
/// expressions. Commas inside nested parentheses (function calls, casts) are
/// preserved; empty entries are dropped.
/// </summary>
internal static class IndexExpressionSplitter
{
    internal static List<string> Split(string content)
    {
        var expressions = new List<string>();
        var current = new StringBuilder();
        var depth = 0;

        foreach (var ch in content)
        {
            switch (ch)
            {
                case '(':
                    depth++;
                    current.Append(ch);
                    break;
                case ')':
                    depth--;
                    current.Append(ch);
                    break;
                case ',' when depth == 0:
                    AddIfNotEmpty(expressions, current);
                    current.Clear();
                    break;
                default:
                    current.Append(ch);
                    break;
            }
        }

        AddIfNotEmpty(expressions, current);
        return expressions;
    }

    private static void AddIfNotEmpty(List<string> expressions, StringBuilder current)
    {
        var expr = current.ToString().Trim();
        if (!string.IsNullOrEmpty(expr))
        {
            expressions.Add(expr);
        }
    }
}
