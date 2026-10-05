using Nimblesite.Lql.Postgres;
using Nimblesite.Lql.SQLite;
using Nimblesite.Lql.SqlServer;
using Nimblesite.Sql.Model;
using Xunit;
using LqlStatementError = Outcome.Result<
    Nimblesite.Lql.Core.LqlStatement,
    Nimblesite.Sql.Model.SqlError
>.Error<Nimblesite.Lql.Core.LqlStatement, Nimblesite.Sql.Model.SqlError>;
using LqlStatementOk = Outcome.Result<
    Nimblesite.Lql.Core.LqlStatement,
    Nimblesite.Sql.Model.SqlError
>.Ok<Nimblesite.Lql.Core.LqlStatement, Nimblesite.Sql.Model.SqlError>;
using SqlTextOk = Outcome.Result<string, Nimblesite.Sql.Model.SqlError>.Ok<
    string,
    Nimblesite.Sql.Model.SqlError
>;

namespace Nimblesite.Lql.Tests;

// Implements [LQL-ERROR-TEST-EXECUTION].

/// <summary>
/// Tests for error handling in LQL to PostgreSQL transformation.
/// Tests invalid syntax, malformed queries, and edge cases using Result types.
/// </summary>
public class LqlErrorHandlingTests
{
    [Fact]
    public void EmptyInput_ShouldReturnError() =>
        ErrorContaining(lqlCode: "", expected: "Empty LQL input");

    [Fact]
    public void WhitespaceOnlyInput_ShouldReturnError() =>
        ErrorContaining(lqlCode: "   \n\t   \n   ", expected: "whitespace");

    [Fact]
    public void InvalidSyntax_ShouldReturnError()
    {
        var position = SyntaxError(
            lqlCode: """
            users |> select(invalid syntax here
            """
        );
        Assert.True(position.Line > 0);
        Assert.True(position.Column >= 0);
    }

    [Fact]
    public void InvalidCharacter_ShouldReturnLexerError()
    {
        var position = SyntaxError(
            lqlCode: """
            users |> select(users.id) #
            """
        );
        Assert.True(position.Line > 0);
        Assert.True(position.Column >= 0);
    }

    [Fact]
    public void MissingPipeOperator_ShouldReturnError() =>
        SyntaxError(
            lqlCode: """
            users select(users.id, users.name)
            """
        );

    [Fact]
    public void InvalidTableName_ShouldReturnError() =>
        SyntaxError(
            lqlCode: """
            123_invalid_table |> select(id, name)
            """
        );

    [Fact]
    public void UnclosedParentheses_ShouldReturnError() =>
        SyntaxError(
            lqlCode: """
            users |> select(users.id, users.name
            """
        );

    [Fact]
    public void InvalidJoinSyntax_ShouldReturnError() =>
        SyntaxError(
            lqlCode: """
            users |> join(orders, invalid_join_syntax)
            """
        );

    [Fact]
    public void MissingOnClauseInJoin_ShouldReturnError() =>
        SyntaxError(
            lqlCode: """
            users |> join(orders)
            """
        );

    [Fact]
    public void InvalidFilterFunction_ShouldReturnError() =>
        SyntaxError(
            lqlCode: """
            users |> filter(invalid_filter_function)
            """
        );

    // Implements [LQL-IDENTIFIER-VALIDATION].
    [Theory]
    [InlineData("tenant_members")]
    [InlineData("fhir_patient")]
    public void UnderscorePipelineBase_ShouldParseAsTableName(string tableName)
    {
        var error = ParseError(lqlCode: "123_invalid_table |> select(id)");
        Assert.Contains("identifier", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(error.Position);

        var accepted = LqlStatementConverter.ToStatement($"{tableName} |> select(id, name)");
        var statement = Assert.IsType<LqlStatementOk>(accepted).Value;
        foreach (
            var result in new[]
            {
                statement.ToPostgreSql(),
                statement.ToSqlServer(),
                statement.ToSQLite(),
            }
        )
        {
            var sql = Assert.IsType<SqlTextOk>(result).Value;
            Assert.Contains(tableName, sql, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("SELECT", sql, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("FROM", sql, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void CircularReference_ShouldReturnError() =>
        SyntaxError(
            lqlCode: """
            let a = b |> select(id)
            let b = a |> select(name)
            a
            """
        );

    [Fact]
    public void ErrorMessage_ShouldIncludeLineAndColumn()
    {
        var error = ParseError(
            lqlCode: """
            users |> select(
                users.id,
                users.name,
                invalid_syntax_here
            """
        );
        Assert.Contains("line", error.FormattedMessage, StringComparison.Ordinal);
        Assert.Contains("column", error.FormattedMessage, StringComparison.Ordinal);
        Assert.NotNull(error.Position);
        Assert.True(error.Position.Line > 0);
        Assert.True(error.Position.Column >= 0);
    }

    [Fact]
    public void ValidSyntax_ShouldReturnSuccess()
    {
        var result = LqlStatementConverter.ToStatement(
            lqlCode: """
            users |> select(users.id, users.name)
            """
        );
        var success = Assert.IsType<LqlStatementOk>(result);
        Assert.NotNull(success.Value);
    }

    private static SqlError ParseError(string lqlCode) =>
        Assert.IsType<LqlStatementError>(LqlStatementConverter.ToStatement(lqlCode: lqlCode)).Value;

    private static SqlError ErrorContaining(string lqlCode, string expected)
    {
        var error = ParseError(lqlCode: lqlCode);
        Assert.Contains(
            expectedSubstring: expected,
            actualString: error.Message,
            comparisonType: StringComparison.Ordinal
        );
        return error;
    }

    private static SourcePosition SyntaxError(string lqlCode)
    {
        var error = ErrorContaining(lqlCode: lqlCode, expected: "Syntax error");
        Assert.NotNull(@object: error.Position);
        return error.Position;
    }
}
