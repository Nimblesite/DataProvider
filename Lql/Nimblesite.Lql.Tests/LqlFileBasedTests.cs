using Nimblesite.Lql.Postgres;
using Nimblesite.Lql.SQLite;
using Nimblesite.Lql.SqlServer;
using Nimblesite.Sql.Model;
using Outcome;
using static global::Tests.Shared.StringAssertions;

namespace Nimblesite.Lql.Tests;

/// <summary>
/// File-based tests for LQL transformation on SQLite, PostgreSQL, and SQL Server.
/// Tests read LQL input and expected SQL output from external files.
/// </summary>
public partial class LqlFileBasedTests
{
    // Implements [LQL-PIPELINE-COMPOSITION] for the shared file corpus.
    private static readonly string TestDataDirectory = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory,
        "TestData"
    );

    /// <summary>
    /// Shared method to execute file-based tests
    /// </summary>
    /// <param name="testCaseName">Name of the test case</param>
    /// <param name="dialect">SQL dialect</param>
    private static void ExecuteFileBasedTest(string testCaseName, string dialect)
    {
        // Arrange
        string lqlFile = Path.Combine(TestDataDirectory, "Lql", $"{testCaseName}.lql");
        string expectedSqlFile = Path.Combine(
            TestDataDirectory,
            "ExpectedSql",
            dialect,
            $"{testCaseName}.sql"
        );

        Assert.True(File.Exists(lqlFile), $"LQL test file {lqlFile} should exist");
        Assert.True(
            File.Exists(expectedSqlFile),
            $"Expected SQL file {expectedSqlFile} should exist"
        );

        string lqlCode = File.ReadAllText(lqlFile);
        string expectedSql = File.ReadAllText(expectedSqlFile).Trim();
        Assert.False(string.IsNullOrWhiteSpace(lqlCode), $"Empty LQL input: {testCaseName}");
        Assert.False(
            string.IsNullOrWhiteSpace(expectedSql),
            $"Expected SQL fixture {expectedSqlFile} must not be empty"
        );

        // Act
        var statementResult = LqlStatementConverter.ToStatement(lqlCode);
        var statement = global::Tests
            .Shared.ResultAssertions.ExactOk(actual: statementResult)
            .Value;

        var sqlResult = ToDialectSql(statement, dialect);
        var actualSql = global::Tests.Shared.ResultAssertions.ExactOk(actual: sqlResult).Value;

        Assert.False(
            string.IsNullOrWhiteSpace(actualSql),
            $"Transpiled SQL for {testCaseName} ({dialect}) must not be empty"
        );

        Assert.Equal(expectedSql, actualSql);
    }

    private static Result<string, SqlError> ToDialectSql(LqlStatement statement, string dialect) =>
        dialect switch
        {
            "PostgreSql" => statement.ToPostgreSql(),
            "SqlServer" => statement.ToSqlServer(),
            "SQLite" => statement.ToSQLite(),
            _ => UnknownDialect(dialect, statement),
        };

    private static Result<string, SqlError> UnknownDialect(string dialect, LqlStatement statement)
    {
        Assert.Fail($"Unsupported dialect: {dialect}");
        return statement.ToSQLite();
    }

    /// <summary>
    /// Every LQL corpus input must have an expected SQL fixture for all three
    /// supported dialects: PostgreSql, SqlServer, and SQLite.
    /// </summary>
    [Fact]
    public void GetAllFileBasedTests_ShouldHaveMatchingFiles()
    {
        // Arrange — fixture directories are part of the corpus and must exist.
        string lqlDirectory = Path.Combine(TestDataDirectory, "Lql");
        string postgreSqlDirectory = Path.Combine(TestDataDirectory, "ExpectedSql", "PostgreSql");
        string sqlServerDirectory = Path.Combine(TestDataDirectory, "ExpectedSql", "SqlServer");
        string sqliteDirectory = Path.Combine(TestDataDirectory, "ExpectedSql", "SQLite");

        Assert.True(
            Directory.Exists(lqlDirectory),
            $"LQL corpus directory {lqlDirectory} should exist"
        );
        Assert.True(
            Directory.Exists(postgreSqlDirectory),
            $"PostgreSql fixture directory {postgreSqlDirectory} should exist"
        );
        Assert.True(
            Directory.Exists(sqlServerDirectory),
            $"SqlServer fixture directory {sqlServerDirectory} should exist"
        );
        Assert.True(
            Directory.Exists(sqliteDirectory),
            $"SQLite fixture directory {sqliteDirectory} should exist"
        );

        // Act
        var (lqlFiles, postgreSqlFiles) = VerifyReusableWorkflow1(
            input0: lqlDirectory,
            input1: postgreSqlDirectory,
            value0: "*.lql"
        );

        var (sqlServerFiles, sqliteFiles) = VerifyReusableWorkflow1(
            input0: sqlServerDirectory,
            input1: sqliteDirectory,
            value0: "*.sql"
        );

        // Assert — exact fixture set equality for every dialect.
        Assert.True(
            lqlFiles.SetEquals(postgreSqlFiles),
            $"Every LQL test file should have a corresponding PostgreSQL expected SQL file. LQL-only: {string.Join(", ", lqlFiles.Except(postgreSqlFiles))}; PostgreSQL-only: {string.Join(", ", postgreSqlFiles.Except(lqlFiles))}"
        );

        Assert.True(
            lqlFiles.SetEquals(sqlServerFiles),
            $"Every LQL test file should have a corresponding SQL Server expected SQL file. LQL-only: {string.Join(", ", lqlFiles.Except(sqlServerFiles))}; SQL Server-only: {string.Join(", ", sqlServerFiles.Except(lqlFiles))}"
        );

        Assert.True(
            lqlFiles.SetEquals(sqliteFiles),
            $"Every LQL test file should have a corresponding SQLite expected SQL file. LQL-only: {string.Join(", ", lqlFiles.Except(sqliteFiles))}; SQLite-only: {string.Join(", ", sqliteFiles.Except(lqlFiles))}"
        );

        Assert.Equal(27, lqlFiles.Count);
    }

    [Theory]
    [MemberData(nameof(LargeQuery_TranspilesCompleteShapeForEveryDialectInlineCases))]
    public void LargeQuery_TranspilesCompleteShapeForEveryDialect(string dialect)
    {
        // Arrange
        const string largeLqlCode = Data_LargeQuery_TranspilesCompleteShapeForEveryDialect_1;

        var sql = VerifyReusableWorkflow3(
            input0: largeLqlCode,
            input1: ToDialectSql,
            input2: dialect
        );
        ContainsAll(
            actual: sql,
            expected: ["SELECT", "JOIN", "GROUP BY", "ORDER BY"],
            comparison: StringComparison.OrdinalIgnoreCase
        );
    }

    [Theory]
    [MemberData(nameof(RepeatedQueries_GenerateStableSqlForEveryDialectInlineCases))]
    public void RepeatedQueries_GenerateStableSqlForEveryDialect(string dialect)
    {
        // Arrange
        const string lqlCode = """
            users |> join(orders, on = users.id = orders.user_id) |> select(users.name, orders.total)
            """;

        string? baseline = null;
        for (int i = 0; i < 1000; i++)
        {
            var sql = VerifyReusableWorkflow3(
                input0: lqlCode,
                input1: ToDialectSql,
                input2: dialect
            );
            ContainsAll(
                actual: sql,
                expected: ["users", "orders"],
                comparison: StringComparison.OrdinalIgnoreCase
            );
            if (baseline is string original)
            {
                Assert.Equal(original, sql);
            }
            else
            {
                baseline = sql;
            }
        }
        Assert.NotNull(baseline);
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    internal static (HashSet<string>, HashSet<string>) VerifyReusableWorkflow1(
        string input0,
        string input1,
        string value0
    )
    {
        // Act
        var lqlFiles = Directory
            .GetFiles(input0, value0)
            .Select(f => Path.GetFileNameWithoutExtension(f))
            .ToHashSet();

        var postgreSqlFiles = Directory
            .GetFiles(input1, "*.sql")
            .Select(f => Path.GetFileNameWithoutExtension(f))
            .ToHashSet();

        return (lqlFiles, postgreSqlFiles);
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    internal static string VerifyReusableWorkflow3(
        string input0,
        Func<LqlStatement, string, Result<string, SqlError>> input1,
        string input2
    )
    {
        var statementResult = LqlStatementConverter.ToStatement(input0);
        var statement = global::Tests
            .Shared.ResultAssertions.ExactOk(actual: statementResult)
            .Value;
        var sql = global::Tests
            .Shared.ResultAssertions.ExactOk(actual: input1(arg1: statement, arg2: input2))
            .Value;
        Assert.False(string.IsNullOrWhiteSpace(sql), input2);

        return sql;
    }
}
