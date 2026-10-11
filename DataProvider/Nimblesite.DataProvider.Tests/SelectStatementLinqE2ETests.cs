// Implements [CI-TEST-SHARED-COMMAND].

using Nimblesite.Lql.SQLite;
using static global::Tests.Shared.StringAssertions;

namespace Nimblesite.DataProvider.Tests;

/// <summary>
/// E2E tests: LINQ expression tree overloads -> ToSQLite -> execute against real SQLite DB.
/// These exercise SelectStatementLinqExtensions and the internal SelectStatementVisitor.
/// </summary>
public sealed partial class SelectStatementLinqE2ETests : IDisposable
{
    private sealed record TestProduct
    {
        public string Id { get; init; } = "";
        public string Name { get; init; } = "";
        public double Price { get; init; }
        public int Quantity { get; init; }
        public string Category { get; init; } = "";
    }

    private readonly SqliteTestDatabase _db = new("linq_e2e");
    private SqliteConnection _connection => _db.Connection;

    public SelectStatementLinqE2ETests()
    {
        CreateSchemaAndSeed();
    }

    public void Dispose() => _db.Dispose();

    private void CreateSchemaAndSeed()
    {
        DatabaseCommands.Execute(
            connection: _connection,
            configure: command => command.CommandText = Data_CreateSchemaAndSeed_1
        );

        DatabaseCommands.Execute(
            connection: _connection,
            configure: command => command.CommandText = Data_CreateSchemaAndSeed_2
        );
    }

    [Fact]
    public void WhereGeneric_SimpleEquality_ReturnsMatchingRows()
    {
        var statement = "Products"
            .From()
            .SelectAll()
            .Where<TestProduct>(p => p.Category == "Tools")
            .ToSqlStatement();

        var sql = ToSqliteSql(statement);
        global::Nimblesite.DataProvider.Tests.QueryBuilderE2ETests.VerifyReusableWorkflow1(
            input0: sql,
            value0: "WHERE",
            value2: "Category"
        );

        var queryResult = _connection.Query<TestProduct>(
            sql: sql,
            mapper: r => new TestProduct
            {
                Id = r.GetString(0),
                Name = r.GetString(1),
                Price = r.GetDouble(2),
                Quantity = r.GetInt32(3),
                Category = r.GetString(4),
            }
        );

        var ok = global::Tests.Shared.ResultAssertions.ExactOk(actual: queryResult);

        var rows = ok.Value;
        Assert.Equal(3, rows.Count);
        Assert.Contains(rows, r => r.Name == "Tool Alpha");
        Assert.Contains(rows, r => r.Name == "Tool Beta");
        Assert.Contains(rows, r => r.Name == "Tool Gamma");
        Assert.All(rows, r => Assert.Equal("Tools", r.Category));
    }

    [Fact]
    public void WhereGeneric_GreaterThanComparison_ReturnsMatchingRows()
    {
        var statement = ProductsWithNameAndPrice()
            .Where<TestProduct>(p => p.Price > 50.0)
            .OrderBy(columnName: "Price")
            .ToSqlStatement();

        var sql = ToSqliteSql(statement);
        Assert.Contains(">", sql);

        var rows = VerifySharedContinuation1(
            input0: sql,
            value0: 3,
            value1: "Gadget X",
            value3: 99.99,
            value5: "Gadget Y",
            value6: 1
        );
        Assert.Equal("Premium Widget", rows[2].Name);
    }

    [Fact]
    public void WhereGeneric_AndCondition_ReturnsMatchingRows()
    {
        var statement = ProductsWithNameAndPrice()
            .Where<TestProduct>(p => p.Price > 10 && p.Category == "Tools")
            .OrderBy(columnName: "Price")
            .ToSqlStatement();

        var rows = VerifyReusableWorkflow12(
            input0: ToSqliteSql,
            input1: statement,
            input2: VerifySharedContinuation1,
            value0: "AND",
            value2: 3,
            value3: "Tool Gamma",
            value4: 15.00,
            value5: "Tool Alpha"
        );
        Assert.Equal("Tool Beta", rows[2].Name);
    }

    [Fact]
    public void WhereGeneric_OrCondition_ReturnsMatchingRows()
    {
        var statement = ProductsWithNameAndPrice()
            .Where<TestProduct>(p => p.Price < 20 || p.Price > 100)
            .OrderBy(columnName: "Price")
            .ToSqlStatement();

        var rows = VerifyReusableWorkflow12(
            input0: ToSqliteSql,
            input1: statement,
            input2: VerifySharedContinuation1,
            value0: "OR",
            value2: 4,
            value3: "Widget A",
            value4: 10.00,
            value5: "Tool Gamma"
        );
        Assert.Equal(15.00, rows[1].Price);
        Assert.Equal("Gadget Y", rows[2].Name);
        Assert.Equal(149.99, rows[2].Price);
        Assert.Equal("Premium Widget", rows[3].Name);
        Assert.Equal(500.00, rows[3].Price);
    }

    [Fact]
    public void WhereGeneric_StringContains_GeneratesLikeClause() =>
        AssertLikeClause(predicate: p => p.Name.Contains("Widget"), fragment: "Widget");

    [Fact]
    public void WhereGeneric_StringStartsWith_GeneratesLikeClause() =>
        AssertLikeClause(predicate: p => p.Name.StartsWith("Tool"), fragment: "Tool");

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private static void AssertLikeClause(
        System.Linq.Expressions.Expression<Func<TestProduct, bool>> predicate,
        string fragment
    )
    {
        var statement = "Products"
            .From()
            .Select(columns: [(null, "Name")])
            .Where<TestProduct>(predicate)
            .OrderBy(columnName: "Name")
            .ToSqlStatement();

        var sql = ToSqliteSql(statement);
        global::Nimblesite.DataProvider.Tests.LqlSqliteE2ETests.VerifyReusableWorkflow5(
            input0: sql,
            input1: ContainsAll,
            value0: "LIKE",
            value2: fragment,
            value3: "Name"
        );
    }

    [Fact]
    public void SelectGeneric_SingleColumn_GeneratesCorrectSQL()
    {
        var statement = "Products".From().Select<TestProduct>(p => p.Name).ToSqlStatement();

        var sql = ToSqliteSql(statement);
        Assert.Contains("Name", sql);
        Assert.DoesNotContain("*", sql);
        Assert.Contains("FROM Products", sql);

        var queryResult = _connection.Query<string>(sql: sql, mapper: r => r.GetString(0));

        VerifyReusableWorkflow13(
            input0: queryResult,
            value0: "Expected Ok result",
            value1: 8,
            value2: "Widget A",
            value3: "Premium Widget"
        );
    }

    [Fact]
    public void OrderByGeneric_SingleColumn_ReturnsSortedResults()
    {
        var statement = ProductsWithNameAndPrice()
            .OrderBy<TestProduct>(p => p.Price)
            .ToSqlStatement();

        var sql = ToSqliteSql(statement);
        global::Nimblesite.DataProvider.Tests.QueryBuilderE2ETests.VerifyReusableWorkflow1(
            input0: sql,
            value0: "ORDER BY",
            value2: "Price"
        );

        var rows = VerifyCompletionWorkflow2(
            input0: VerifySharedContinuation1,
            input1: sql,
            value0: 8,
            value1: "Widget A",
            value2: 10.00,
            value3: "Premium Widget",
            value4: 7,
            value5: 500.00,
            value6: 7
        );

        for (var i = 1; i < rows.Count; i++)
        {
            Assert.True(rows[i].Price >= rows[i - 1].Price);
        }
    }

    [Fact]
    public void FullLinqChain_WhereSelectOrderByTakeSkip_ExecutesCorrectly()
    {
        var statement = ProductsWithNameAndPrice()
            .Where<TestProduct>(p => p.Price > 10)
            .OrderBy<TestProduct>(p => p.Price)
            .Skip(count: 1)
            .Take(count: 3)
            .ToSqlStatement();

        var sql = ToSqliteSql(statement);
        ContainsAll(
            actual: sql,
            expected: ["WHERE", "ORDER BY", "LIMIT", "OFFSET"],
            comparison: StringComparison.OrdinalIgnoreCase
        );

        var rows = VerifyCompletionWorkflow2(
            input0: VerifySharedContinuation1,
            input1: sql,
            value0: 3,
            value1: "Widget B",
            value2: 25.50,
            value3: "Tool Alpha",
            value4: 1,
            value5: 35.00,
            value6: 1
        );
        Assert.Equal("Tool Beta", rows[2].Name);
        Assert.Equal(45.00, rows[2].Price);
    }

    /// <summary>
    /// Convert a statement to SQLite SQL, asserting the result is Ok, and return the SQL text.
    /// </summary>
    // Implements [CI-TEST-SHARED-SCENARIOS].
    private static global::Nimblesite.Sql.Model.SelectStatementBuilder ProductsWithNameAndPrice() =>
        "Products".From().Select(columns: [(null, "Name"), (null, "Price")]);

    private static string ToSqliteSql(SelectStatement statement)
    {
        var sqlResult = statement.ToSQLite();
        var sqlOk = global::Tests.Shared.ResultAssertions.ExactOk(actual: sqlResult);
        return sqlOk.Value;
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private IReadOnlyList<(string Name, double Price)> QueryProductsAndAssertCount(
        string input0,
        int value3
    )
    {
        var queryResult = _connection.Query<(string Name, double Price)>(
            sql: input0,
            mapper: r => (r.GetString(0), r.GetDouble(1))
        );

        var ok = global::Tests.Shared.ResultAssertions.ExactOk(actual: queryResult);

        var rows = ok.Value;
        Assert.Equal(value3, rows.Count);

        return rows;
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private IReadOnlyList<(string Name, double Price)> VerifySharedContinuation1(
        string input0,
        int value0,
        string value1,
        double value3,
        string value5,
        int value6
    )
    {
        var rows = QueryProductsAndAssertCount(input0: input0, value3: value0);
        Assert.Equal(value1, rows[0].Name);
        Assert.Equal(value3, rows[0].Price);
        Assert.Equal(value5, rows[value6].Name);

        return rows;
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    // Implements [CI-TEST-SHARED-SCENARIOS].
    private static IReadOnlyList<(string Name, double Price)> VerifyReusableWorkflow12(
        Func<SelectStatement, string> input0,
        SelectStatement input1,
        Func<
            string,
            int,
            string,
            double,
            string,
            int,
            IReadOnlyList<(string Name, double Price)>
        > input2,
        string value0,
        int value2,
        string value3,
        double value4,
        string value5
    )
    {
        var sql = input0(arg: input1);
        Assert.Contains(value0, sql, StringComparison.OrdinalIgnoreCase);
        var rows = input2(
            arg1: sql,
            arg2: value2,
            arg3: value3,
            arg4: value4,
            arg5: value5,
            arg6: 1
        );
        return rows;
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    internal static void VerifyReusableWorkflow13(
        Result<IReadOnlyList<string>, Sql.Model.SqlError> input0,
        string value0,
        int value1,
        string value2,
        string value3
    )
    {
        var ok = global::Tests.Shared.ResultAssertions.Expected<Result<
            IReadOnlyList<string>,
            SqlError
        >.Ok<IReadOnlyList<string>, SqlError>>(actual: input0, message: value0);

        Assert.Equal(value1, ok.Value.Count);
        Assert.Contains(value2, ok.Value);
        Assert.Contains(value3, ok.Value);
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    internal static IReadOnlyList<(string Name, double Price)> VerifyCompletionWorkflow2(
        Func<
            string,
            int,
            string,
            double,
            string,
            int,
            IReadOnlyList<(string Name, double Price)>
        > input0,
        string input1,
        int value0,
        string value1,
        double value2,
        string value3,
        int value4,
        double value5,
        int value6
    )
    {
        var rows = input0(
            arg1: input1,
            arg2: value0,
            arg3: value1,
            arg4: value2,
            arg5: value3,
            arg6: value4
        );
        Assert.Equal(value5, rows[value6].Price);

        return rows;
    }
}
