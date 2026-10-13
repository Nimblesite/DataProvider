using Nimblesite.Lql.Postgres;
using Nimblesite.Lql.SQLite;
using Nimblesite.Lql.SqlServer;
using Nimblesite.Sql.Model;
using static global::Tests.Shared.ResultAssertions;
using static global::Tests.Shared.StringAssertions;
using LqlStatementError = Outcome.Result<
    Nimblesite.Lql.Core.LqlStatement,
    Nimblesite.Sql.Model.SqlError
>.Error<Nimblesite.Lql.Core.LqlStatement, Nimblesite.Sql.Model.SqlError>;

namespace Nimblesite.Lql.Tests;

// Implements [LQL-ERROR-TEST-EXECUTION].

/// <summary>
/// Tests for error handling in LQL to PostgreSQL transformation.
/// Tests invalid syntax, malformed queries, and edge cases using Result types.
/// </summary>
public partial class LqlErrorHandlingTests
{
    [Fact]
    public void EmptyInput_ShouldReturnError() =>
        ErrorContaining(lqlCode: "", expected: "Empty LQL input");

    [Fact]
    public void WhitespaceOnlyInput_ShouldReturnError() =>
        ErrorContaining(lqlCode: "   \n\t   \n   ", expected: "whitespace");

    // Implements [CI-TEST-SHARED-CASE-MATRIX].
    [Theory]
    [MemberData(nameof(IndependentScenarioData9))]
    public void InvalidSyntax_ShouldReturnError_ScenarioCases(
        string scenarioName,
        System.String scenario0
    )
    {
        NamedCase(caseName: scenarioName);

        var position = SyntaxError(lqlCode: scenario0);
        Assert.True(position.Line > 0);
        Assert.True(position.Column >= 0);
    }

    // Implements [CI-TEST-SHARED-CASE-MATRIX].
    [Theory]
    [MemberData(nameof(MissingPipeOperator_ShouldReturnError_CasesData))]
    public void MissingPipeOperator_ShouldReturnError_Cases(string caseName, string lqlCode)
    {
        NamedCase(caseName: caseName);
        SyntaxError(lqlCode: lqlCode);
    }

    // Implements [LQL-IDENTIFIER-VALIDATION].
    [Theory]
    [MemberData(nameof(UnderscorePipelineBase_ShouldParseAsTableNameInlineCases))]
    public void UnderscorePipelineBase_ShouldParseAsTableName(string tableName)
    {
        var error = ParseError(lqlCode: "123_invalid_table |> select(id)");
        Assert.Contains("identifier", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(error.Position);

        var accepted = LqlStatementConverter.ToStatement($"{tableName} |> select(id, name)");
        var statement = global::Tests.Shared.ResultAssertions.ExactOk(actual: accepted).Value;
        foreach (
            var result in new[]
            {
                statement.ToPostgreSql(),
                statement.ToSqlServer(),
                statement.ToSQLite(),
            }
        )
        {
            var sql = global::Tests.Shared.ResultAssertions.ExactOk(actual: result).Value;
            ContainsAll(
                actual: sql,
                expected: [tableName, "SELECT", "FROM"],
                comparison: StringComparison.OrdinalIgnoreCase
            );
        }
    }

    [Fact]
    public void ErrorMessage_ShouldIncludeLineAndColumn()
    {
        var error = ParseError(lqlCode: Data_ErrorMessage_ShouldIncludeLineAndColumn_1);
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
        var success = global::Tests.Shared.ResultAssertions.ExactOk(actual: result);
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
