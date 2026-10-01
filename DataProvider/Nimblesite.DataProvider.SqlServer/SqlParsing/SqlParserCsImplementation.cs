using Nimblesite.Sql.Model;
using Outcome;
using SqlParser;
using SqlParser.Ast;

namespace Nimblesite.DataProvider.SqlServer.SqlParsing;

/// <summary>
/// SQL parser implementation using SqlParserCS library
/// </summary>
internal sealed class SqlParserCsImplementation : ISqlParser
{
    private readonly SqlQueryParser _parser = new();

    /// <summary>
    /// Parses the specified SQL text into a Result containing either a SelectStatement or an error.
    /// </summary>
    /// <param name="sql">The SQL text to parse.</param>
    /// <returns>A Result containing either a SelectStatement or an error message.</returns>
    public Result<SelectStatement, string> ParseSql(string sql)
    {
        try
        {
            var statements = _parser.Parse(sql);
            var parameters = ExtractParameters(statements);
            var parameterInfos = parameters.Select(p => new ParameterInfo(p)).ToList();

            // Determine query type from the first statement
            var queryType = DetermineQueryType(statements);

            var selectStatement = new SelectStatement { Parameters = parameterInfos.ToFrozenSet() };

            return new Result<SelectStatement, string>.Ok<SelectStatement, string>(selectStatement);
        }
        catch (Exception ex)
        {
            return new Result<SelectStatement, string>.Error<SelectStatement, string>(ex.Message);
        }
    }

    private static List<string> ExtractParameters(IList<Statement> statements)
    {
        var parameters = new HashSet<string>();

        // Extract parameters from all statements using a simple visitor pattern
        foreach (var statement in statements)
        {
            ExtractParametersFromStatement(statement, parameters);
        }

        return [.. parameters];
    }

    private static void ExtractParametersFromStatement(
        Statement statement,
        HashSet<string> parameters
    )
    {
        // This is a simplified parameter extraction
        // In a real implementation, we would need to traverse the AST more thoroughly
        var statementString = statement.ToString();

        // Look for common parameter patterns: @param, :param, $param, ?
        var words = statementString.Split([
            ' ',
            '\t',
            '\n',
            '\r',
            '(',
            ')',
            ',',
            '=',
            '<',
            '>',
            '!',
        ]);

        foreach (var word in words)
        {
            if (string.IsNullOrWhiteSpace(word))
                continue;

            var trimmed = word.Trim();
            if (trimmed.StartsWith('@') && trimmed.Length > 1)
            {
                // SQL Server style parameter
                parameters.Add(trimmed[1..]);
            }
            else if (trimmed.StartsWith(':') && trimmed.Length > 1)
            {
                // Oracle/PostgreSQL style parameter
                parameters.Add(trimmed[1..]);
            }
            else if (trimmed.StartsWith('$') && trimmed.Length > 1)
            {
                // PostgreSQL style parameter
                parameters.Add(trimmed[1..]);
            }
            // Note: ? parameters are positional and harder to extract parameter names
        }
    }

    private static string DetermineQueryType(IList<Statement> statements)
    {
        if (!statements.Any())
            return "EMPTY";

        var firstStatement = statements.First();

        return firstStatement switch
        {
            Statement.Select => "SELECT",
            Statement.Insert => "INSERT",
            Statement.Update => "UPDATE",
            Statement.Delete => "DELETE",
            Statement.CreateTable => "CREATE_TABLE",
            Statement.AlterTable => "ALTER_TABLE",
            Statement.Drop => "DROP",
            _ => "OTHER",
        };
    }
}
