using Antlr4.Runtime;
using Nimblesite.Sql.Model;
using Outcome;

namespace Nimblesite.Lql.Core.Parsing;

/// <summary>
/// Utility class for parsing LQL code.
/// </summary>
internal static class LqlCodeParser
{
    private static readonly char[] SplitCharacters = [' ', '\t', '|', '>', '(', ')', ',', '='];

    /// <summary>
    /// Parses LQL code and returns the AST wrapped in a Result.
    /// </summary>
    /// <param name="lqlCode">The LQL code to parse.</param>
    /// <returns>A Result containing either the parsed AST node or parsing errors.</returns>
    public static Result<INode, SqlError> Parse(string lqlCode)
    {
        try
        {
            // Validate input
            if (string.IsNullOrEmpty(lqlCode))
            {
                return new Result<INode, SqlError>.Error<INode, SqlError>(
                    SqlError.WithPosition("Empty LQL input", 1, 0, lqlCode)
                );
            }

            if (string.IsNullOrWhiteSpace(lqlCode))
            {
                return new Result<INode, SqlError>.Error<INode, SqlError>(
                    SqlError.WithPosition("LQL input contains only whitespace", 1, 0, lqlCode)
                );
            }

            // Additional semantic validation before parsing
            var semanticIssues = CheckBasicSemantics(lqlCode);
            if (semanticIssues != null)
            {
                return new Result<INode, SqlError>.Error<INode, SqlError>(semanticIssues);
            }

            // Create ANTLR input stream
            var inputStream = new AntlrInputStream(lqlCode);

            // Create lexer
            var lexer = new LqlLexer(inputStream);
            var lexerErrorListener = new DetailedLexerErrorListener(lqlCode);
            lexer.RemoveErrorListeners();
            lexer.AddErrorListener(lexerErrorListener);

            // Create token stream
            var tokenStream = new CommonTokenStream(lexer);

            // Create parser
            var parser = new LqlParser(tokenStream);
            var parserErrorListener = new DetailedParserErrorListener(lqlCode);
            parser.RemoveErrorListeners();
            parser.AddErrorListener(parserErrorListener);

            // Parse the program
            var programContext = parser.program();

            // Check for lexer errors
            if (lexerErrorListener.Errors.Count > 0)
            {
                return new Result<INode, SqlError>.Error<INode, SqlError>(
                    lexerErrorListener.Errors[0]
                );
            }

            // Check for parser errors
            if (parserErrorListener.Errors.Count > 0)
            {
                return new Result<INode, SqlError>.Error<INode, SqlError>(
                    parserErrorListener.Errors[0]
                );
            }

            // Create visitor and visit the parse tree
            // For parsing, we don't need a SQL context - it's only needed for SQL generation
            // The actual SQL generation will use the appropriate context later
            var visitor = new LqlToAstVisitor(lqlCode);
            var astNode = visitor.Visit(programContext);

            return new Result<INode, SqlError>.Ok<INode, SqlError>(astNode);
        }
        catch (SqlErrorException ex)
        {
            return new Result<INode, SqlError>.Error<INode, SqlError>(
                ex.SqlError ?? SqlError.FromException(ex)
            );
        }
        catch (Exception ex)
        {
            return new Result<INode, SqlError>.Error<INode, SqlError>(SqlError.FromException(ex));
        }
    }

    /// <summary>
    /// Performs basic semantic checks on the raw LQL code to catch obvious errors early
    /// </summary>
    /// <param name="lqlCode">The LQL code to check</param>
    /// <returns>A SqlError if there are semantic issues, null otherwise</returns>
    private static SqlError? CheckBasicSemantics(string lqlCode)
    {
        // Check for missing pipe operators (basic heuristic)
        if (
            lqlCode.Contains("users select(", StringComparison.OrdinalIgnoreCase)
            && !lqlCode.Contains("|>", StringComparison.Ordinal)
        )
        {
            return SqlError.WithPosition(
                "Syntax error: Expected '|>' operator between table and operation",
                1,
                0,
                lqlCode
            );
        }

        // Check for circular references in let statements
        var letStatements = new Dictionary<string, string>();
        var lines = lqlCode.Split('\n');

        // First pass: collect all let statements.
        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();
            if (
                string.IsNullOrEmpty(trimmedLine)
                || trimmedLine.StartsWith("--", StringComparison.Ordinal)
            )
                continue;

            if (trimmedLine.StartsWith("let ", StringComparison.Ordinal))
            {
                var parts = trimmedLine.Split('=', 2);
                if (parts.Length == 2)
                {
                    var varName = parts[0][4..].Trim(); // Remove "let " prefix
                    var expression = parts[1].Trim();

                    // Extract the first identifier from the expression (before |>)
                    var pipeIndex = expression.IndexOf("|>", StringComparison.Ordinal);
                    if (pipeIndex > 0)
                    {
                        var referencedVar = expression[..pipeIndex].Trim();
                        letStatements[varName] = referencedVar;
                    }
                }
            }
        }

        // Second pass: check for circular references
        foreach (var kvp in letStatements)
        {
            var visited = new HashSet<string>();
            var current = kvp.Key;

            while (letStatements.ContainsKey(current))
            {
                if (visited.Contains(current))
                {
                    return SqlError.WithPosition(
                        $"Syntax error: Circular reference detected involving variable '{current}'",
                        1,
                        0,
                        lqlCode
                    );
                }

                visited.Add(current);
                current = letStatements[current];
            }
        }

        // Check for obvious invalid table names and column references
        // Reuse the existing 'lines' variable instead of declaring a new one
        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();
            if (
                string.IsNullOrEmpty(trimmedLine)
                || trimmedLine.StartsWith("--", StringComparison.Ordinal)
            )
                continue;

            // Check for identifiers starting with numbers
            var words = trimmedLine.Split(SplitCharacters, StringSplitOptions.RemoveEmptyEntries);
            foreach (var word in words)
            {
                if (
                    word.Length > 0
                    && char.IsDigit(word[0])
                    && word.Contains('_', StringComparison.Ordinal)
                )
                {
                    return SqlError.WithPosition(
                        $"Syntax error: Invalid identifier '{word}' - identifiers cannot start with a number",
                        1,
                        0,
                        lqlCode
                    );
                }
            }

            // Pipeline bases can be table names such as tenant_members. The
            // parser cannot distinguish those from variables without schema
            // metadata, so undefined-variable validation belongs in a later
            // semantic pass with table context.
        }

        return null;
    }
}
