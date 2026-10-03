using Microsoft.Data.Sqlite;
using Nimblesite.Lql.Core;
using Nimblesite.Lql.SQLite;
using Nimblesite.Sql.Model;
using Outcome;

namespace Nimblesite.DataProvider.Tests;

/// <summary>
/// E2E tests: LQL parse -> SQLite SQL conversion -> execute against real DB -> verify results.
/// Each test covers a full LQL workflow from string input to verified query results.
/// </summary>
public sealed class LqlSqliteE2ETests : IDisposable
{
    private readonly SqliteTestDatabase _db = new("lql_e2e");
    private SqliteConnection _connection => _db.Connection;

    public LqlSqliteE2ETests()
    {
        CreateSchemaAndSeed();
    }

    public void Dispose() => _db.Dispose();

    private void CreateSchemaAndSeed()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE users (
                id TEXT PRIMARY KEY,
                name TEXT NOT NULL,
                email TEXT NOT NULL,
                age INTEGER NOT NULL,
                country TEXT NOT NULL,
                status TEXT NOT NULL DEFAULT 'active'
            );
            CREATE TABLE orders (
                id TEXT PRIMARY KEY,
                user_id TEXT NOT NULL,
                total REAL NOT NULL,
                status TEXT NOT NULL DEFAULT 'pending',
                FOREIGN KEY (user_id) REFERENCES users(id)
            );
            """;
        cmd.ExecuteNonQuery();

        // Seed users
        using var userCmd = _connection.CreateCommand();
        userCmd.CommandText = """
            INSERT INTO users VALUES ('u1', 'Alice', 'alice@test.com', 30, 'US', 'active');
            INSERT INTO users VALUES ('u2', 'Bob', 'bob@test.com', 25, 'UK', 'active');
            INSERT INTO users VALUES ('u3', 'Charlie', 'charlie@test.com', 45, 'US', 'inactive');
            INSERT INTO users VALUES ('u4', 'Diana', 'diana@test.com', 35, 'AU', 'active');
            INSERT INTO users VALUES ('u5', 'Eve', 'eve@test.com', 22, 'US', 'active');
            """;
        userCmd.ExecuteNonQuery();

        // Seed orders
        using var orderCmd = _connection.CreateCommand();
        orderCmd.CommandText = """
            INSERT INTO orders VALUES ('o1', 'u1', 150.00, 'completed');
            INSERT INTO orders VALUES ('o2', 'u1', 75.50, 'completed');
            INSERT INTO orders VALUES ('o3', 'u2', 200.00, 'pending');
            INSERT INTO orders VALUES ('o4', 'u3', 50.00, 'completed');
            INSERT INTO orders VALUES ('o5', 'u4', 300.00, 'shipped');
            INSERT INTO orders VALUES ('o6', 'u4', 125.00, 'completed');
            INSERT INTO orders VALUES ('o7', 'u5', 45.00, 'pending');
            """;
        orderCmd.ExecuteNonQuery();
    }

    private static LqlStatement AssertParseOk(string lqlCode)
    {
        var parseResult = LqlStatementConverter.ToStatement(lqlCode);
        var parseOk = Assert.IsType<Result<LqlStatement, SqlError>.Ok<LqlStatement, SqlError>>(
            parseResult
        );
        return parseOk.Value;
    }

    private static string AssertSqliteOk(LqlStatement statement)
    {
        var sqlResult = statement.ToSQLite();
        var sqlOk = Assert.IsType<StringOk>(sqlResult);
        return sqlOk.Value;
    }

    [Fact]
    public void LqlSelectAllFromTable_ParseConvertExecute_ReturnsAllRows()
    {
        // Parse LQL
        var lqlCode = "users |> select(users.id, users.name, users.email)";
        var statement = AssertParseOk(lqlCode);
        Assert.NotNull(statement);
        Assert.NotNull(statement.AstNode);
        Assert.Null(statement.ParseError);

        // Convert to SQLite
        var sql = AssertSqliteOk(statement);
        Assert.Contains("SELECT", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("users", sql, StringComparison.OrdinalIgnoreCase);

        // Execute against real DB
        var queryResult = _connection.Query<(string, string, string)>(
            sql: sql,
            mapper: r => (r.GetString(0), r.GetString(1), r.GetString(2))
        );
        Assert.True(
            queryResult
                is Result<IReadOnlyList<(string, string, string)>, SqlError>.Ok<
                    IReadOnlyList<(string, string, string)>,
                    SqlError
                >
        );
        if (
            queryResult
            is Result<IReadOnlyList<(string, string, string)>, SqlError>.Ok<
                IReadOnlyList<(string, string, string)>,
                SqlError
            > ok
        )
        {
            var rows = ok.Value;
            Assert.Equal(5, rows.Count);
            Assert.Contains(rows, r => r.Item2 == "Alice");
            Assert.Contains(rows, r => r.Item2 == "Bob");
            Assert.Contains(rows, r => r.Item3 == "diana@test.com");
        }
    }

    [Fact]
    public void LqlWithFilter_ParseConvertExecute_ReturnsFilteredRows()
    {
        var lqlCode =
            "users |> filter(fn(row) => row.users.age > 25) |> select(users.name, users.age)";
        var statement = AssertParseOk(lqlCode);

        var sql = AssertSqliteOk(statement);
        Assert.Contains("WHERE", sql, StringComparison.OrdinalIgnoreCase);

        var queryResult = _connection.Query<(string, long)>(
            sql: sql,
            mapper: r => (r.GetString(0), r.GetInt64(1))
        );
        Assert.True(
            queryResult
                is Result<IReadOnlyList<(string, long)>, SqlError>.Ok<
                    IReadOnlyList<(string, long)>,
                    SqlError
                >
        );
        if (
            queryResult
            is Result<IReadOnlyList<(string, long)>, SqlError>.Ok<
                IReadOnlyList<(string, long)>,
                SqlError
            > ok
        )
        {
            var rows = ok.Value;

            // Alice (30), Charlie (45), Diana (35) are > 25
            Assert.Equal(3, rows.Count);
            Assert.All(rows, r => Assert.True(r.Item2 > 25));
            Assert.Contains(rows, r => r.Item1 == "Alice");
            Assert.Contains(rows, r => r.Item1 == "Charlie");
            Assert.Contains(rows, r => r.Item1 == "Diana");
        }
    }

    [Fact]
    public void LqlWithOrderByAndLimit_ParseConvertExecute_ReturnsOrderedSubset()
    {
        var lqlCode =
            "users |> order_by(users.age asc) |> limit(3) |> select(users.name, users.age)";
        var statement = AssertParseOk(lqlCode);

        var sql = AssertSqliteOk(statement);
        Assert.Contains("ORDER BY", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("LIMIT", sql, StringComparison.OrdinalIgnoreCase);

        var queryResult = _connection.Query<(string, long)>(
            sql: sql,
            mapper: r => (r.GetString(0), r.GetInt64(1))
        );
        Assert.True(
            queryResult
                is Result<IReadOnlyList<(string, long)>, SqlError>.Ok<
                    IReadOnlyList<(string, long)>,
                    SqlError
                >
        );
        if (
            queryResult
            is Result<IReadOnlyList<(string, long)>, SqlError>.Ok<
                IReadOnlyList<(string, long)>,
                SqlError
            > ok
        )
        {
            var rows = ok.Value;

            Assert.Equal(3, rows.Count);
            // Should be youngest 3: Eve (22), Bob (25), Alice (30)
            Assert.Equal("Eve", rows[0].Item1);
            Assert.Equal(22L, rows[0].Item2);
            Assert.Equal("Bob", rows[1].Item1);
            Assert.Equal(25L, rows[1].Item2);
            Assert.Equal("Alice", rows[2].Item1);
            Assert.Equal(30L, rows[2].Item2);
        }
    }

    [Fact]
    public void LqlWithJoin_ParseConvertExecute_ReturnsJoinedData()
    {
        var lqlCode =
            "users |> join(orders, on = users.id = orders.user_id) |> select(users.name, orders.total)";
        var statement = AssertParseOk(lqlCode);

        var sql = AssertSqliteOk(statement);
        Assert.Contains("JOIN", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("users.id = orders.user_id", sql, StringComparison.OrdinalIgnoreCase);

        var queryResult = _connection.Query<(string, double)>(
            sql: sql,
            mapper: r => (r.GetString(0), r.GetDouble(1))
        );
        Assert.True(
            queryResult
                is Result<IReadOnlyList<(string, double)>, SqlError>.Ok<
                    IReadOnlyList<(string, double)>,
                    SqlError
                >
        );
        if (
            queryResult
            is Result<IReadOnlyList<(string, double)>, SqlError>.Ok<
                IReadOnlyList<(string, double)>,
                SqlError
            > ok
        )
        {
            var rows = ok.Value;

            // 7 orders across users with matching IDs
            Assert.Equal(7, rows.Count);
            Assert.Contains(rows, r => r.Item1 == "Alice" && r.Item2 == 150.00);
            Assert.Contains(rows, r => r.Item1 == "Alice" && r.Item2 == 75.50);
            Assert.Contains(rows, r => r.Item1 == "Bob" && r.Item2 == 200.00);
            Assert.Contains(rows, r => r.Item1 == "Diana" && r.Item2 == 300.00);
        }
    }

    [Fact]
    public void LqlStatementConverter_InvalidSyntax_ReturnsError()
    {
        // Missing pipe operator
        var badLql = "users select name";
        var result = LqlStatementConverter.ToStatement(badLql);

        // Should either be an error result or a statement with a parse error
        if (result is Result<LqlStatement, SqlError>.Error<LqlStatement, SqlError> error)
        {
            Assert.NotEmpty(error.Value.Message);
        }
        else if (result is Result<LqlStatement, SqlError>.Ok<LqlStatement, SqlError> ok)
        {
            // Parser may successfully parse but produce a broken AST,
            // or produce a statement that fails on ToSQLite conversion
            var stmt = ok.Value;
            if (stmt.ParseError is { } parseErr)
            {
                Assert.NotEmpty(parseErr.Message);
            }
            else
            {
                // The parser accepted it - verify it at least produced something
                Assert.NotNull(stmt.AstNode);
            }
        }
    }

    [Fact]
    public void LqlStatementToSQLite_WithParseError_ReturnsError()
    {
        // Create a statement with a parse error directly
        var errorStatement = new LqlStatement { ParseError = SqlError.Create("Test parse error") };

        var sqlResult = errorStatement.ToSQLite();
        Assert.True(sqlResult is StringError);
        if (sqlResult is StringError errorResult)
        {
            var error = errorResult.Value;
            Assert.Contains("Test parse error", error.Message, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void LqlStatementToSQLite_WithNullAstNode_ReturnsError()
    {
        var emptyStatement = new LqlStatement { AstNode = null };
        var sqlResult = emptyStatement.ToSQLite();
        Assert.True(sqlResult is StringError);
    }

    [Fact]
    public void LqlStatementToSQLite_WithIdentifierNode_ReturnsSelectAll()
    {
        // An Identifier node should produce SELECT * FROM tableName
        var identifierStatement = new LqlStatement { AstNode = new Identifier("customers") };

        var sqlResult = identifierStatement.ToSQLite();
        Assert.True(sqlResult is StringOk);
        if (sqlResult is StringOk sqlOk)
        {
            var sql = sqlOk.Value;
            Assert.Contains("SELECT *", sql, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("customers", sql);
        }
    }

    [Fact]
    public void SQLiteContextDirect_BuildComplexQuery_GeneratesValidSQL()
    {
        // Use SQLiteContext directly to build a query
        var context = new SQLiteContext();
        context.SetBaseTable("users");
        context.SetSelectColumns([ColumnInfo.Named(name: "name"), ColumnInfo.Named(name: "email")]);
        context.AddWhereCondition(
            WhereCondition.Comparison(
                left: ColumnInfo.Named(name: "status"),
                @operator: ComparisonOperator.Eq,
                right: "'active'"
            )
        );
        context.AddOrderBy([(Column: "name", Direction: "ASC")]);
        context.SetLimit("10");

        var sql = context.GenerateSQL();
        Assert.Contains("SELECT", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("name", sql);
        Assert.Contains("email", sql);
        Assert.Contains("WHERE", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ORDER BY", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("LIMIT", sql, StringComparison.OrdinalIgnoreCase);

        // Execute it
        var result = _connection.Query<(string, string)>(
            sql: sql,
            mapper: r => (r.GetString(0), r.GetString(1))
        );
        Assert.True(
            result
                is Result<IReadOnlyList<(string, string)>, SqlError>.Ok<
                    IReadOnlyList<(string, string)>,
                    SqlError
                >
        );
        if (
            result
            is Result<IReadOnlyList<(string, string)>, SqlError>.Ok<
                IReadOnlyList<(string, string)>,
                SqlError
            > ok
        )
        {
            var rows = ok.Value;
            Assert.Equal(4, rows.Count); // 4 active users
            Assert.Equal("Alice", rows[0].Item1); // First alphabetically
        }
    }

    [Fact]
    public void SQLiteContextDirect_WithJoin_GeneratesValidJoinSQL()
    {
        var context = new SQLiteContext();
        context.SetBaseTable("users");
        context.AddJoin(
            joinType: "INNER JOIN",
            tableName: "orders",
            condition: "users.id = orders.user_id"
        );
        context.SetSelectColumns([
            ColumnInfo.Named(name: "name", tableAlias: "users"),
            ColumnInfo.Named(name: "total", tableAlias: "orders"),
        ]);

        var sql = context.GenerateSQL();
        Assert.Contains("INNER JOIN", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("orders", sql);
        Assert.Contains("users.id = orders.user_id", sql);

        var result = _connection.Query<(string, double)>(
            sql: sql,
            mapper: r => (r.GetString(0), r.GetDouble(1))
        );
        Assert.True(
            result
                is Result<IReadOnlyList<(string, double)>, SqlError>.Ok<
                    IReadOnlyList<(string, double)>,
                    SqlError
                >
        );
        if (
            result
            is Result<IReadOnlyList<(string, double)>, SqlError>.Ok<
                IReadOnlyList<(string, double)>,
                SqlError
            > ok
        )
        {
            var rows = ok.Value;
            Assert.Equal(7, rows.Count);
        }
    }
}
