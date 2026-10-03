using Nimblesite.Lql.Postgres;
using Nimblesite.Lql.SQLite;
using Nimblesite.Lql.SqlServer;
using Nimblesite.Sql.Model;
using Outcome;
using Xunit;
using LqlStatementOk = Outcome.Result<
    Nimblesite.Lql.Core.LqlStatement,
    Nimblesite.Sql.Model.SqlError
>.Ok<Nimblesite.Lql.Core.LqlStatement, Nimblesite.Sql.Model.SqlError>;
using SqlTextOk = Outcome.Result<string, Nimblesite.Sql.Model.SqlError>.Ok<
    string,
    Nimblesite.Sql.Model.SqlError
>;

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
        var statement = Assert.IsType<LqlStatementOk>(statementResult).Value;

        var sqlResult = ToDialectSql(statement, dialect);
        var actualSql = Assert.IsType<SqlTextOk>(sqlResult).Value;

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
        var lqlFiles = Directory
            .GetFiles(lqlDirectory, "*.lql")
            .Select(f => Path.GetFileNameWithoutExtension(f))
            .ToHashSet();

        var postgreSqlFiles = Directory
            .GetFiles(postgreSqlDirectory, "*.sql")
            .Select(f => Path.GetFileNameWithoutExtension(f))
            .ToHashSet();

        var sqlServerFiles = Directory
            .GetFiles(sqlServerDirectory, "*.sql")
            .Select(f => Path.GetFileNameWithoutExtension(f))
            .ToHashSet();

        var sqliteFiles = Directory
            .GetFiles(sqliteDirectory, "*.sql")
            .Select(f => Path.GetFileNameWithoutExtension(f))
            .ToHashSet();

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
    [InlineData("PostgreSql")]
    [InlineData("SqlServer")]
    [InlineData("SQLite")]
    public void LargeQuery_TranspilesCompleteShapeForEveryDialect(string dialect)
    {
        // Arrange
        const string largeLqlCode = """
            -- Complex query with multiple operations
            let base_users =
                users
                |> join(user_profiles, on = users.id = user_profiles.user_id)
                |> join(user_settings, on = users.id = user_settings.user_id)
                |> filter(fn(row) => row.users.status = 'active' and row.user_profiles.verified = true)

            let user_orders =
                base_users
                |> join(orders, on = users.id = orders.user_id)
                |> join(order_items, on = orders.id = order_items.order_id)
                |> join(products, on = order_items.product_id = products.id)
                |> join(categories, on = products.category_id = categories.id)

            let aggregated_data =
                user_orders
                |> group_by(users.id, users.name, categories.name)
                |> select(
                    users.id,
                    users.name,
                    categories.name as category_name,
                    count(distinct orders.id) as order_count,
                    sum(order_items.quantity * products.price) as total_spent,
                    avg(products.rating) as avg_product_rating
                )
                |> having(fn(group) => count(distinct orders.id) > 5)

            aggregated_data
            |> filter(fn(row) => row.total_spent > 1000)
            |> order_by(total_spent desc, order_count desc)
            |> limit(100)
            """;

        var statementResult = LqlStatementConverter.ToStatement(largeLqlCode);
        var statement = Assert.IsType<LqlStatementOk>(statementResult).Value;
        var sql = Assert.IsType<SqlTextOk>(ToDialectSql(statement, dialect)).Value;
        Assert.False(string.IsNullOrWhiteSpace(sql), dialect);
        Assert.Contains("SELECT", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("JOIN", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("GROUP BY", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ORDER BY", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("PostgreSql")]
    [InlineData("SqlServer")]
    [InlineData("SQLite")]
    public void RepeatedQueries_GenerateStableSqlForEveryDialect(string dialect)
    {
        // Arrange
        const string lqlCode = """
            users |> join(orders, on = users.id = orders.user_id) |> select(users.name, orders.total)
            """;

        string? baseline = null;
        for (int i = 0; i < 1000; i++)
        {
            var statementResult = LqlStatementConverter.ToStatement(lqlCode);
            var statement = Assert.IsType<LqlStatementOk>(statementResult).Value;
            var sql = Assert.IsType<SqlTextOk>(ToDialectSql(statement, dialect)).Value;
            Assert.False(string.IsNullOrWhiteSpace(sql), dialect);
            Assert.Contains("users", sql, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("orders", sql, StringComparison.OrdinalIgnoreCase);
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
}
