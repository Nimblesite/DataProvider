namespace Nimblesite.DataProvider.Migration.Core;

/// <summary>
/// Text helpers for comparing SQL expressions read back from database catalogs.
/// Implements [MIG-VERIFY-BEFORE-UP-TO-DATE].
/// </summary>
public static class SqlExpressionText
{
    /// <summary>
    /// Catalogs store defaults, filters and checks with or without redundant
    /// parentheses, e.g. <c>(now())</c> vs <c>now()</c> or SQL Server's <c>((0))</c>.
    /// Remove every pair that encloses the whole expression.
    /// </summary>
    public static string StripOuterParens(string sql)
    {
        var text = sql.Trim();
        while (text.Length >= 2 && text[0] == '(' && ClosesAtEnd(text))
        {
            text = text[1..^1].Trim();
        }
        return text;
    }

    private static bool ClosesAtEnd(string text)
    {
        var depth = 0;
        for (var i = 0; i < text.Length; i++)
        {
            depth += text[i] switch
            {
                '(' => 1,
                ')' => -1,
                _ => 0,
            };
            if (depth == 0)
            {
                return i == text.Length - 1;
            }
        }
        return false;
    }
}
