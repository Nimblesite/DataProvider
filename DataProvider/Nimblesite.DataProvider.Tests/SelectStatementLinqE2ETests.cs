// Implements [CI-TEST-SHARED-COMMAND].
using Microsoft.Data.Sqlite;
using Nimblesite.Lql.SQLite;
using Nimblesite.Sql.Model;
using Outcome;
using Tests.Shared;
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
        Assert.Contains("WHERE", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Category", sql);

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

        var ok = global::Tests.Shared.ResultAssertions.Expected<Result<
            IReadOnlyList<TestProduct>,
            SqlError
        >.Ok<IReadOnlyList<TestProduct>, SqlError>>(
            actual: queryResult,
            message: "Expected Ok result"
        );

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
        var statement = "Products"
            .From()
            .Select(columns: [(null, "Name"), (null, "Price")])
            .Where<TestProduct>(p => p.Price > 50.0)
            .OrderBy(columnName: "Price")
            .ToSqlStatement();

        var sql = ToSqliteSql(statement);
        Assert.Contains(">", sql);

        var rows = QueryProductsAndAssertCount(input0: sql, value3: 3);
        Assert.Equal("Gadget X", rows[0].Name);
        Assert.Equal(99.99, rows[0].Price);
        Assert.Equal("Gadget Y", rows[1].Name);
        Assert.Equal("Premium Widget", rows[2].Name);
    }

    [Fact]
    public void WhereGeneric_AndCondition_ReturnsMatchingRows()
    {
        var statement = "Products"
            .From()
            .Select(columns: [(null, "Name"), (null, "Price")])
            .Where<TestProduct>(p => p.Price > 10 && p.Category == "Tools")
            .OrderBy(columnName: "Price")
            .ToSqlStatement();

        var sql = ToSqliteSql(statement);
        Assert.Contains("AND", sql, StringComparison.OrdinalIgnoreCase);

        var rows = QueryProductsAndAssertCount(input0: sql, value3: 3);
        Assert.Equal("Tool Gamma", rows[0].Name);
        Assert.Equal(15.00, rows[0].Price);
        Assert.Equal("Tool Alpha", rows[1].Name);
        Assert.Equal("Tool Beta", rows[2].Name);
    }

    [Fact]
    public void WhereGeneric_OrCondition_ReturnsMatchingRows()
    {
        var statement = "Products"
            .From()
            .Select(columns: [(null, "Name"), (null, "Price")])
            .Where<TestProduct>(p => p.Price < 20 || p.Price > 100)
            .OrderBy(columnName: "Price")
            .ToSqlStatement();

        var sql = ToSqliteSql(statement);
        Assert.Contains("OR", sql, StringComparison.OrdinalIgnoreCase);

        var rows = QueryProductsAndAssertCount(input0: sql, value3: 4);
        Assert.Equal("Widget A", rows[0].Name);
        Assert.Equal(10.00, rows[0].Price);
        Assert.Equal("Tool Gamma", rows[1].Name);
        Assert.Equal(15.00, rows[1].Price);
        Assert.Equal("Gadget Y", rows[2].Name);
        Assert.Equal(149.99, rows[2].Price);
        Assert.Equal("Premium Widget", rows[3].Name);
        Assert.Equal(500.00, rows[3].Price);
    }

    [Fact]
    public void WhereGeneric_StringContains_GeneratesLikeClause()
    {
        var statement = "Products"
            .From()
            .Select(columns: [(null, "Name")])
            .Where<TestProduct>(p => p.Name.Contains("Widget"))
            .OrderBy(columnName: "Name")
            .ToSqlStatement();

        var sql = ToSqliteSql(statement);
        Assert.Contains("LIKE", sql, StringComparison.OrdinalIgnoreCase);
        ContainsAll(actual: sql, expected: ["Widget", "Name"]);
    }

    [Fact]
    public void WhereGeneric_StringStartsWith_GeneratesLikeClause()
    {
        var statement = "Products"
            .From()
            .Select(columns: [(null, "Name")])
            .Where<TestProduct>(p => p.Name.StartsWith("Tool"))
            .OrderBy(columnName: "Name")
            .ToSqlStatement();

        var sql = ToSqliteSql(statement);
        Assert.Contains("LIKE", sql, StringComparison.OrdinalIgnoreCase);
        ContainsAll(actual: sql, expected: ["Tool", "Name"]);
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

        var ok = global::Tests.Shared.ResultAssertions.Expected<Result<
            IReadOnlyList<string>,
            SqlError
        >.Ok<IReadOnlyList<string>, SqlError>>(actual: queryResult, message: "Expected Ok result");

        Assert.Equal(8, ok.Value.Count);
        Assert.Contains("Widget A", ok.Value);
        Assert.Contains("Premium Widget", ok.Value);
    }

    [Fact]
    public void OrderByGeneric_SingleColumn_ReturnsSortedResults()
    {
        var statement = "Products"
            .From()
            .Select(columns: [(null, "Name"), (null, "Price")])
            .OrderBy<TestProduct>(p => p.Price)
            .ToSqlStatement();

        var sql = ToSqliteSql(statement);
        Assert.Contains("ORDER BY", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Price", sql);

        var rows = QueryProductsAndAssertCount(input0: sql, value3: 8);
        Assert.Equal("Widget A", rows[0].Name);
        Assert.Equal(10.00, rows[0].Price);
        Assert.Equal("Premium Widget", rows[7].Name);
        Assert.Equal(500.00, rows[7].Price);

        for (var i = 1; i < rows.Count; i++)
        {
            Assert.True(rows[i].Price >= rows[i - 1].Price);
        }
    }

    [Fact]
    public void FullLinqChain_WhereSelectOrderByTakeSkip_ExecutesCorrectly()
    {
        var statement = "Products"
            .From()
            .Select(columns: [(null, "Name"), (null, "Price")])
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

        var rows = QueryProductsAndAssertCount(input0: sql, value3: 3);

        // Products with Price > 10 ordered by Price ASC:
        // Tool Gamma (15), Widget B (25.5), Tool Alpha (35), Tool Beta (45),
        // Gadget X (99.99), Gadget Y (149.99), Premium Widget (500)
        // Skip 1 -> Widget B, Take 3 -> Widget B, Tool Alpha, Tool Beta
        Assert.Equal("Widget B", rows[0].Name);
        Assert.Equal(25.50, rows[0].Price);
        Assert.Equal("Tool Alpha", rows[1].Name);
        Assert.Equal(35.00, rows[1].Price);
        Assert.Equal("Tool Beta", rows[2].Name);
        Assert.Equal(45.00, rows[2].Price);
    }

    /// <summary>
    /// Convert a statement to SQLite SQL, asserting the result is Ok, and return the SQL text.
    /// </summary>
    private static string ToSqliteSql(SelectStatement statement)
    {
        var sqlResult = statement.ToSQLite();
        var sqlOk = Assert.IsType<StringOk>(sqlResult);
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

        var ok = global::Tests.Shared.ResultAssertions.Expected<Result<
            IReadOnlyList<(string Name, double Price)>,
            SqlError
        >.Ok<IReadOnlyList<(string Name, double Price)>, SqlError>>(
            actual: queryResult,
            message: "Expected Ok result"
        );

        var rows = ok.Value;
        Assert.Equal(value3, rows.Count);

        return rows;
    }
}
