using Antlr4.Runtime.Tree;

namespace Nimblesite.DataProvider.Core.Parsing;

// Implements [CON-SHARED-CORE]. Generic ANTLR parse-tree walker that extracts
// parameter names from any vendored SQL grammar. Used by SQLite and Postgres
// parsers so neither dialect duplicates the extraction logic.
/// <summary>
/// Extracts parameter names from an ANTLR parse tree, regardless of dialect.
/// Supports positional (<c>?</c>), named (<c>:name</c>), SQL Server style
/// (<c>@name</c>), and Postgres style (<c>$name</c>) parameters.
/// </summary>
public static class AntlrSqlParameterExtractor
{
    /// <summary>
    /// Extracts parameter names from the provided ANTLR <see cref="IParseTree"/>.
    /// </summary>
    /// <param name="parseTree">The ANTLR parse tree.</param>
    /// <returns>A list of discovered parameter names, in first-seen order.</returns>
    public static List<string> ExtractParameters(IParseTree parseTree)
    {
        var parameters = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        ExtractParametersRecursive(parseTree, parameters, seen);
        return parameters;
    }

    private static void ExtractParametersRecursive(
        IParseTree node,
        List<string> parameters,
        HashSet<string> seen
    )
    {
        if (node is ITerminalNode terminal)
        {
            TryAddParameter(terminal.GetText(), parameters, seen);
        }

        for (int i = 0; i < node.ChildCount; i++)
        {
            ExtractParametersRecursive(node.GetChild(i), parameters, seen);
        }
    }

    private static void TryAddParameter(string text, List<string> parameters, HashSet<string> seen)
    {
        if (string.IsNullOrEmpty(text))
            return;

        if (text.StartsWith('?'))
        {
            var positional = $"param{parameters.Count + 1}";
            if (seen.Add(positional))
                parameters.Add(positional);
            return;
        }

        if (text.Length < 2)
            return;

        var prefix = text[0];
        if (prefix != ':' && prefix != '@' && prefix != '$')
            return;

        var paramName = text[1..];
        if (!IsValidParameterName(paramName))
            return;

        if (seen.Add(paramName))
            parameters.Add(paramName);
    }

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="name"/> is a valid
    /// SQL parameter identifier (starts with a letter or underscore, followed
    /// by letters, digits or underscores only). Shared with dialect parsers
    /// that scan raw token streams for grammar-invisible parameters.
    /// </summary>
    /// <param name="name">The candidate parameter name.</param>
    /// <returns><see langword="true"/> when the name is a valid identifier.</returns>
    internal static bool IsValidParameterName(string name)
    {
        if (string.IsNullOrEmpty(name))
            return false;

        if (!char.IsLetter(name[0]) && name[0] != '_')
            return false;

        foreach (var c in name)
        {
            if (!char.IsLetterOrDigit(c) && c != '_')
                return false;
        }

        return true;
    }
}
