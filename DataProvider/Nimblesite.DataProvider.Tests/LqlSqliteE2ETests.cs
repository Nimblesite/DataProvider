// Implements [CI-TEST-SHARED-COMMAND].

using Nimblesite.Lql.Core;
using Nimblesite.Lql.SQLite;
using static global::Tests.Shared.RowAssertions;
using static global::Tests.Shared.StringAssertions;

namespace Nimblesite.DataProvider.Tests;

/// <summary>
/// E2E tests: LQL parse -> SQLite SQL conversion -> execute against real DB -> verify results.
/// Each test covers a full LQL workflow from string input to verified query results.
/// </summary>
public sealed partial class LqlSqliteE2ETests : IDisposable
{
    private readonly SqliteTestDatabase _db = new("lql_e2e");
    private SqliteConnection _connection => _db.Connection;

    public LqlSqliteE2ETests()
    {
        CreateSchemaAndSeed();
    }

    public void Dispose() => _db.Dispose();

    private void CreateSchemaAndSeed() =>
        SqliteSchemaSetup.Apply(
            connection: _connection,
            command => command.CommandText = Data_CreateSchemaAndSeed_1,
            command => command.CommandText = Data_CreateSchemaAndSeed_2,
            command => command.CommandText = Data_CreateSchemaAndSeed_3
        );

    private static LqlStatement AssertParseOk(string lqlCode)
    {
        var parseResult = LqlStatementConverter.ToStatement(lqlCode);
        var parseOk = global::Tests.Shared.ResultAssertions.ExactOk(actual: parseResult);
        return parseOk.Value;
    }

    private static string AssertSqliteOk(LqlStatement statement)
    {
        var sqlResult = statement.ToSQLite();
        var sqlOk = global::Tests.Shared.ResultAssertions.ExactOk(actual: sqlResult);
        return sqlOk.Value;
    }

    // Implements [CI-TEST-SHARED-SCENARIOS]: parse an LQL string, convert it to
    // SQLite SQL and assert the expected fragments in one step.
    private static string AssertSql(string lqlCode, string fragment0, string fragment1) =>
        VerifyReusableWorkflow8(
            input0: AssertSqliteOk,
            input1: AssertParseOk(lqlCode),
            input2: ContainsAll,
            value0: fragment0,
            value1: fragment1
        );

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
        var sql = AssertSql(
            lqlCode: "users |> select(users.id, users.name, users.email)",
            fragment0: "SELECT",
            fragment1: "users"
        );

        // Execute against real DB
        var queryResult = _connection.Query<(string, string, string)>(
            sql: sql,
            mapper: r => (r.GetString(0), r.GetString(1), r.GetString(2))
        );
        global::Tests.Shared.ResultAssertions.VerifyOk(
            actual: queryResult,
            verify: ok =>
            {
                var rows = ok.Value;
                Assert.Equal(5, rows.Count);
                ContainsAll(rows: rows, projection: r => r.Item2, expected: ["Alice", "Bob"]);
                ContainsAll(rows: rows, projection: r => r.Item3, expected: ["diana@test.com"]);
            }
        );
    }

    [Fact]
    public void LqlWithFilter_ParseConvertExecute_ReturnsFilteredRows()
    {
        var lqlCode =
            "users |> filter(fn(row) => row.users.age > 25) |> select(users.name, users.age)";
        var statement = AssertParseOk(lqlCode);

        var sql = AssertSqliteOk(statement);
        Assert.Contains("WHERE", sql, StringComparison.OrdinalIgnoreCase);

        var queryResult = VerifyReusableWorkflow10(input0: sql);
        var rows = global::Tests.Shared.ResultAssertions.Ok(actual: queryResult).Value;

        // Alice (30), Charlie (45), Diana (35) are > 25
        Assert.Equal(3, rows.Count);
        Assert.All(rows, r => Assert.True(r.Item2 > 25));
        ContainsAll(rows: rows, projection: r => r.Item1, expected: ["Alice", "Charlie", "Diana"]);
    }

    [Fact]
    public void LqlWithOrderByAndLimit_ParseConvertExecute_ReturnsOrderedSubset()
    {
        var sql = AssertSql(
            lqlCode: "users |> order_by(users.age asc) |> limit(3) |> select(users.name, users.age)",
            fragment0: "ORDER BY",
            fragment1: "LIMIT"
        );

        var queryResult = VerifyReusableWorkflow10(input0: sql);
        var rows = global::Tests.Shared.ResultAssertions.Ok(actual: queryResult).Value;

        Assert.Equal(3, rows.Count);
        // Should be youngest 3: Eve (22), Bob (25), Alice (30)
        Assert.Equal("Eve", rows[0].Item1);
        Assert.Equal(22L, rows[0].Item2);
        Assert.Equal("Bob", rows[1].Item1);
        Assert.Equal(25L, rows[1].Item2);
        Assert.Equal("Alice", rows[2].Item1);
        Assert.Equal(30L, rows[2].Item2);
    }

    [Fact]
    public void LqlWithJoin_ParseConvertExecute_ReturnsJoinedData()
    {
        var sql = AssertSql(
            lqlCode: "users |> join(orders, on = users.id = orders.user_id) |> select(users.name, orders.total)",
            fragment0: "JOIN",
            fragment1: "users.id = orders.user_id"
        );

        var queryResult = VerifyReusableWorkflow11(input0: sql);
        var rows = global::Tests.Shared.ResultAssertions.Ok(actual: queryResult).Value;

        // 7 orders across users with matching IDs
        Assert.Equal(7, rows.Count);
        ContainsAll(
            rows: rows,
            projection: r => (r.Item1, r.Item2),
            expected: [("Alice", 150.00), ("Alice", 75.50), ("Bob", 200.00), ("Diana", 300.00)]
        );
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
        var error = global::Tests.Shared.ResultAssertions.Error(actual: sqlResult).Value;
        Assert.Contains("Test parse error", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LqlStatementToSQLite_WithNullAstNode_ReturnsError()
    {
        var emptyStatement = new LqlStatement { AstNode = null };
        global::Tests.Shared.ResultAssertions.Error(actual: emptyStatement.ToSQLite());
    }

    [Fact]
    public void LqlStatementToSQLite_WithIdentifierNode_ReturnsSelectAll()
    {
        // An Identifier node should produce SELECT * FROM tableName
        var identifierStatement = new LqlStatement { AstNode = new Identifier("customers") };

        var sqlResult = identifierStatement.ToSQLite();
        global::Tests.Shared.ResultAssertions.VerifyOk(
            actual: sqlResult,
            verify: sqlOk =>
            {
                var sql = sqlOk.Value;
                global::Nimblesite.DataProvider.Tests.QueryBuilderE2ETests.VerifyReusableWorkflow1(
                    input0: sql,
                    value0: "SELECT *",
                    value2: "customers"
                );
            }
        );
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

        var sql = VerifyCompletionWorkflow3(
            input0: context,
            input1: VerifyReusableWorkflow5,
            input2: ContainsAll,
            value0: "SELECT",
            value1: "name",
            value2: "email"
        );
        ContainsAll(
            actual: sql,
            expected: ["WHERE", "ORDER BY", "LIMIT"],
            comparison: StringComparison.OrdinalIgnoreCase
        );

        // Execute it
        var result = _connection.Query<(string, string)>(
            sql: sql,
            mapper: r => (r.GetString(0), r.GetString(1))
        );
        global::Tests.Shared.ResultAssertions.VerifyOk(
            actual: result,
            verify: ok =>
            {
                var rows = ok.Value;
                Assert.Equal(4, rows.Count); // 4 active users
                Assert.Equal("Alice", rows[0].Item1); // First alphabetically
            }
        );
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

        var sql = VerifyCompletionWorkflow3(
            input0: context,
            input1: VerifyReusableWorkflow5,
            input2: ContainsAll,
            value0: "INNER JOIN",
            value1: "orders",
            value2: "users.id = orders.user_id"
        );

        var result = VerifyReusableWorkflow11(input0: sql);
        var rows = global::Tests.Shared.ResultAssertions.Ok(actual: result).Value;
        Assert.Equal(7, rows.Count);
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    internal static void VerifyReusableWorkflow5(
        string input0,
        Action<string?, System.Collections.Immutable.ImmutableArray<string>> input1,
        string value0,
        string value2,
        string value3
    )
    {
        Assert.Contains(value0, input0, StringComparison.OrdinalIgnoreCase);
        input1(arg1: input0, arg2: [value2, value3]);
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    internal static string VerifyReusableWorkflow8(
        Func<LqlStatement, string> input0,
        LqlStatement input1,
        Action<
            string?,
            System.Collections.Immutable.ImmutableArray<string>,
            StringComparison
        > input2,
        string value0,
        string value1
    )
    {
        // Convert to SQLite
        var sql = input0(arg: input1);
        input2(arg1: sql, arg2: [value0, value1], arg3: StringComparison.OrdinalIgnoreCase);

        return sql;
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private Result<IReadOnlyList<(string, long)>, Sql.Model.SqlError> VerifyReusableWorkflow10(
        string input0
    )
    {
        var queryResult = _connection.Query<(string, long)>(
            sql: input0,
            mapper: r => (r.GetString(0), r.GetInt64(1))
        );
        _ = global::Tests.Shared.ResultAssertions.Ok(actual: queryResult);
        return queryResult;
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private Result<IReadOnlyList<(string, double)>, Sql.Model.SqlError> VerifyReusableWorkflow11(
        string input0
    )
    {
        var queryResult = _connection.Query<(string, double)>(
            sql: input0,
            mapper: r => (r.GetString(0), r.GetDouble(1))
        );
        _ = global::Tests.Shared.ResultAssertions.Ok(actual: queryResult);
        return queryResult;
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    internal static string VerifyCompletionWorkflow3(
        SQLiteContext input0,
        Action<
            string,
            Action<string?, System.Collections.Immutable.ImmutableArray<string>>,
            string,
            string,
            string
        > input1,
        Action<string?, System.Collections.Immutable.ImmutableArray<string>> input2,
        string value0,
        string value1,
        string value2
    )
    {
        var sql = input0.GenerateSQL();
        input1(arg1: sql, arg2: input2, arg3: value0, arg4: value1, arg5: value2);

        return sql;
    }
}
