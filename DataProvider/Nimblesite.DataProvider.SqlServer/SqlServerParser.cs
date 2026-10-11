using System.Collections.Frozen;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using Nimblesite.Sql.Model;
using ModelStatement = Nimblesite.Sql.Model.SelectStatement;
using ParseResult = Outcome.Result<Nimblesite.Sql.Model.SelectStatement, string>;

namespace Nimblesite.DataProvider.SqlServer;

/// <summary>Parses T-SQL with Microsoft's official parser. Implements [CON-PARSER-ONLY].</summary>
public static class SqlServerParser
{
    /// <summary>Validates SQL and extracts external parameters, excluding comments and literals.</summary>
    public static ParseResult ParseSql(string sql)
    {
        try
        {
            using var input = new StringReader(sql);
            var fragment = new TSql170Parser(initialQuotedIdentifiers: true).Parse(
                input,
                out var errors
            );
            return errors.Count > 0
                ? new ParseResult.Error<ModelStatement, string>(
                    string.Join("; ", errors.Select(error => error.Message))
                )
                : new ParseResult.Ok<ModelStatement, string>(BuildStatement(fragment));
        }
        catch (Exception exception)
        {
            return new ParseResult.Error<ModelStatement, string>(exception.ToString());
        }
    }

    private static ModelStatement BuildStatement(TSqlFragment fragment)
    {
        var locals = Descendants(fragment: fragment)
            .OfType<DeclareVariableStatement>()
            .SelectMany(statement => statement.Declarations)
            .Select(declaration => declaration.VariableName.Value)
            .ToFrozenSet(StringComparer.OrdinalIgnoreCase);
        return new ModelStatement
        {
            Parameters = fragment
                .ScriptTokenStream.Where(token =>
                    token.TokenType == TSqlTokenType.Variable && !locals.Contains(token.Text)
                )
                .Select(token => token.Text.TrimStart('@'))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(name => new ParameterInfo(Name: name))
                .ToFrozenSet(),
        };
    }

    private static IEnumerable<TSqlFragment> Descendants(TSqlFragment fragment) =>
        fragment
            .GetType()
            .GetProperties()
            .Where(property => property.CanRead && property.GetIndexParameters().Length == 0)
            .SelectMany(property =>
                property.GetValue(obj: fragment) switch
                {
                    TSqlFragment child => Descendants(fragment: child),
                    IEnumerable<TSqlFragment> children => children.SelectMany(Descendants),
                    _ => [],
                }
            )
            .Prepend(fragment);
}
