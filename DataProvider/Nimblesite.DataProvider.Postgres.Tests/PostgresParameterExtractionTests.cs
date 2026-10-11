using Nimblesite.DataProvider.Postgres.Parsing;
using Nimblesite.Sql.Model;
using Outcome;
using Xunit;
using static global::Tests.Shared.ResultAssertions;

namespace Nimblesite.DataProvider.Postgres.Tests;

// Postgres-only parameter tests. The shared SqlParserContractTests covers
// the full @name contract; this file holds edge cases that are specific to
// the Postgres lexer's tokenisation + Postgres's native $N positional
// parameter form.
public sealed class PostgresParameterExtractionTests
{
    private static List<string> Extract(string sql)
    {
        var result = new PostgresAntlrParser().ParseSql(sql);

        var stmt = Ok(
            actual: result,
            message: result is Result<SelectStatement, string>.Error<SelectStatement, string> err
                ? $"Parse failed: {err.Value}"
                : "Parse failed"
        ).Value;
        return stmt.Parameters.Select(p => p.Name).ToList();
    }

    [Fact]
    public void Dollar_numeric_positional_does_not_crash()
    {
        // $1 is Postgres's native positional parameter. The Core extractor
        // rejects purely-numeric names (must start with letter or
        // underscore), so the param list should not contain "1" itself.
        // The important guarantee is that ParseSql doesn't throw.
        var p = Extract("SELECT * FROM users WHERE id = $1");
        Assert.DoesNotContain("1", p);
    }

    [Fact]
    public void Space_after_at_is_not_a_parameter()
    {
        // `@ id` (with a space) is NOT a parameter — the Operator and the
        // Identifier aren't adjacent in the token stream, so the Postgres-
        // specific @name scanner skips it.
        var p = Extract("SELECT 1 WHERE x = @ id");
        Assert.DoesNotContain("id", p);
    }

    [Fact]
    public void At_followed_by_reserved_keyword_is_extracted()
    {
        // `@name`, `@limit`, `@offset` all tokenise as Operator('@')
        // followed by a reserved keyword token (NAME_P, LIMIT, OFFSET
        // respectively). The Postgres scanner accepts any adjacent word-
        // like token, not just Identifier.
        var p = Extract("SELECT id FROM users LIMIT @limit OFFSET @offset");
        Assert.Contains("limit", p);
        Assert.Contains("offset", p);
    }
}
