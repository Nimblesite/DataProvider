using System.Collections.Frozen;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using Nimblesite.Sql.Model;
using Outcome;
using SelectStatement = Nimblesite.Sql.Model.SelectStatement;

namespace Nimblesite.DataProvider.SqlServer.SqlParsing;

// Parsing T-SQL with anything other than the official Microsoft parser is
// illegal per project rules. This implementation uses
// Microsoft.SqlServer.TransactSql.ScriptDom (ScriptDom) exclusively: the AST
// visitor collects variable references instead of any textual scanning.
/// <summary>
/// T-SQL parser implementation built on the official
/// Microsoft.SqlServer.TransactSql.ScriptDom parser.
/// </summary>
internal sealed class SqlParserCsImplementation : ISqlParser
{
    private readonly TSql160Parser _parser = new(initialQuotedIdentifiers: true);

    /// <summary>
    /// Parses the specified T-SQL text into a Result containing either a SelectStatement or an error.
    /// </summary>
    /// <param name="sql">The SQL text to parse.</param>
    /// <returns>A Result containing either a SelectStatement or an error message.</returns>
    public Result<SelectStatement, string> ParseSql(string sql)
    {
        var fragment = _parser.Parse(new StringReader(sql), out var errors);

        if (errors is { Count: > 0 })
        {
            return FormatErrors(errors);
        }

        if (fragment is not TSqlScript script)
        {
            return ParseFailure("SQL parsing produced no syntax tree");
        }

        var statements = script.Batches.SelectMany(batch => batch.Statements).ToList().AsReadOnly();

        return new Result<SelectStatement, string>.Ok<SelectStatement, string>(
            new SelectStatement { Parameters = ExtractParameters(statements) }
        );
    }

    /// <summary>
    /// Collects the distinct variable references across all statements,
    /// excluding variables declared inside the script (locals, table variables).
    /// </summary>
    /// <param name="statements">The parsed T-SQL statements.</param>
    /// <returns>The discovered script parameters as a frozen set.</returns>
    private static FrozenSet<ParameterInfo> ExtractParameters(
        IReadOnlyList<TSqlStatement> statements
    )
    {
        var declared = CollectDeclaredVariables(statements);
        var collector = new VariableReferenceCollector();
        foreach (var statement in statements)
        {
            statement.AcceptChildren(collector);
        }

        return collector
            .Names.Where(name => !declared.Contains(name))
            .Select(name => new ParameterInfo(name))
            .ToFrozenSet();
    }

    /// <summary>
    /// Collects the names of all variables declared by DECLARE statements.
    /// </summary>
    /// <param name="statements">The parsed T-SQL statements.</param>
    /// <returns>Declared variable names without their @ prefix.</returns>
    private static FrozenSet<string> CollectDeclaredVariables(
        IReadOnlyList<TSqlStatement> statements
    ) =>
        statements
            .OfType<DeclareVariableStatement>()
            .SelectMany(declaration => declaration.Declarations)
            .Select(element => element.VariableName.Value.TrimStart('@'))
            .ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Formats ScriptDom parse errors into a single error message.
    /// </summary>
    /// <param name="errors">The parse errors reported by ScriptDom.</param>
    /// <returns>A failed Result describing every parse error.</returns>
    private static Result<SelectStatement, string> FormatErrors(IList<ParseError> errors) =>
        ParseFailure(
            string.Join(
                "; ",
                errors.Select(error => $"({error.Line},{error.Column}): {error.Message}")
            )
        );

    /// <summary>
    /// Builds a failed parse Result.
    /// </summary>
    /// <param name="message">The failure description.</param>
    /// <returns>A failed Result containing the message.</returns>
    private static Result<SelectStatement, string> ParseFailure(string message) =>
        new Result<SelectStatement, string>.Error<SelectStatement, string>(message);

    /// <summary>
    /// Determines whether the script consists exclusively of INSERT, UPDATE, or
    /// DELETE statements (no result set to model) using the official ScriptDom
    /// parser.
    /// </summary>
    /// <param name="sql">The SQL text to classify.</param>
    /// <returns>True when every statement is a non-query statement.</returns>
    internal static bool IsNonQuery(string sql)
    {
        var parser = new TSql160Parser(initialQuotedIdentifiers: true);
        var fragment = parser.Parse(new StringReader(sql), out var errors);

        if (errors is { Count: > 0 } || fragment is not TSqlScript script)
        {
            return false;
        }

        var statements = script.Batches.SelectMany(batch => batch.Statements).ToList();

        return statements.Count > 0
            && statements.All(statement =>
                statement switch
                {
                    InsertStatement insert => insert.InsertSpecification.OutputClause is null,
                    UpdateStatement update => update.UpdateSpecification.OutputClause is null,
                    DeleteStatement delete => delete.DeleteSpecification.OutputClause is null,
                    _ => false,
                }
            );
    }

    /// <summary>
    /// ScriptDom visitor that collects <see cref="VariableReference"/>
    /// names (without the @ prefix) across a statement tree.
    /// </summary>
    private sealed class VariableReferenceCollector : TSqlFragmentVisitor
    {
        /// <summary>
        /// Gets the collected variable names, de-duplicated case-insensitively.
        /// </summary>
        public HashSet<string> Names { get; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Collects the referenced variable's name.
        /// </summary>
        /// <param name="node">The variable reference fragment.</param>
        public override void ExplicitVisit(VariableReference node) =>
            Names.Add(node.Name.TrimStart('@'));
    }
}
