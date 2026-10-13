using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using Nimblesite.DataProvider.Core.Parsing;
using Nimblesite.Sql.Model;
using Outcome;

namespace Nimblesite.DataProvider.Postgres.Parsing;

// Implements [CON-PARSER-ONLY]. Mirrors Nimblesite.DataProvider.SQLite's
// SqliteAntlrParser in shape. One Postgres-specific wrinkle: the vendored
// antlr/grammars-v4 PostgreSQL grammar only models `$N` positional
// parameters (PARAM: '$' [0-9]+) and has no rule for `@name` or `:name`
// style parameters. The lexer therefore tokenises `@id` as two separate
// tokens — Operator(@) followed by Identifier(id). We handle this by
// scanning the token stream directly for adjacent Operator(@) + Identifier
// pairs, alongside the generic Core walker for the parse-tree-reachable
// cases ($1-style tokens).
/// <summary>
/// PostgreSQL parser built on the vendored antlr/grammars-v4 PostgreSQL
/// grammar. Extracts parameters both from the parse tree (Core walker) and
/// from the raw token stream (Postgres-specific @name detection).
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class PostgresAntlrParser : ISqlParser
{
    /// <inheritdoc />
    public Result<SelectStatement, string> ParseSql(string sql)
    {
        try
        {
            var inputStream = new AntlrInputStream(sql);
            var lexer = new PostgreSQLLexer(inputStream);
            var tokens = new CommonTokenStream(lexer);
            tokens.Fill();

            var parser = new PostgreSQLParser(tokens);
            var parseTree = parser.root();

            var parameters = ExtractAllParameters(tokens, parseTree);
            var parameterInfos = parameters.Select(p => new ParameterInfo(p)).ToList();

            var statement = new SelectStatement { Parameters = parameterInfos.ToFrozenSet() };
            return new Result<SelectStatement, string>.Ok<SelectStatement, string>(statement);
        }
        catch (Exception ex)
        {
            return new Result<SelectStatement, string>.Error<SelectStatement, string>(
                $"Failed to parse SQL: {ex}"
            );
        }
    }

    private static List<string> ExtractAllParameters(CommonTokenStream tokens, IParseTree parseTree)
    {
        // Start with anything the shared Core walker can see (covers $1-style
        // PARAM tokens and any :name / ? forms if a future grammar bump adds
        // them).
        var parameters = AntlrSqlParameterExtractor.ExtractParameters(parseTree);
        var seen = new HashSet<string>(parameters, StringComparer.Ordinal);

        // Scan the raw token stream for Postgres's grammar-invisible @name
        // form. The lexer tokenises `@id` as Operator('@') + <word-like
        // token>('id') with no whitespace between them (their start/stop
        // indices are adjacent). The word-like token may be Identifier OR a
        // keyword token like NAME_P, LIMIT, OFFSET — Postgres has ~400
        // reserved/unreserved keywords and any of them can legitimately be a
        // parameter name in generated code. We accept anything whose text is
        // a valid identifier literal.
        var all = tokens.GetTokens();
        for (var i = 0; i < all.Count - 1; i++)
        {
            var current = all[i];
            if (current.Type != PostgreSQLLexer.Operator || current.Text != "@")
                continue;

            var next = all[i + 1];

            // Adjacent means no whitespace or other character between them.
            if (next.StartIndex != current.StopIndex + 1)
                continue;

            var name = next.Text;
            if (!AntlrSqlParameterExtractor.IsValidParameterName(name))
                continue;

            if (seen.Add(name))
                parameters.Add(name);
        }

        return parameters;
    }
}
