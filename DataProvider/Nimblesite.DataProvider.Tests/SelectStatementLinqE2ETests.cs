using Microsoft.Data.Sqlite;
using Nimblesite.Lql.SQLite;
using Nimblesite.Sql.Model;
using Outcome;

namespace Nimblesite.DataProvider.Tests;

/// <summary>
/// E2E tests: LINQ expression tree overloads -> ToSQLite -> execute against real SQLite DB.
/// These exercise SelectStatementLinqExtensions and the internal SelectStatementVisitor.
/// </summary>
public sealed class SelectStatementLinqE2ETests : IDisposable
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
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE Products (
                Id TEXT PRIMARY KEY,
                Name TEXT NOT NULL,
                Price REAL NOT NULL,
                Quantity INTEGER NOT NULL,
                Category TEXT NOT NULL
            );
            """;
        cmd.ExecuteNonQuery();

        using var seedCmd = _connection.CreateCommand();
        seedCmd.CommandText = """
            INSERT INTO Products VALUES ('p1', 'Widget A', 10.00, 100, 'Electronics');
            INSERT INTO Products VALUES ('p2', 'Widget B', 25.50, 50, 'Electronics');
            INSERT INTO Products VALUES ('p3', 'Gadget X', 99.99, 10, 'Gadgets');
            INSERT INTO Products VALUES ('p4', 'Gadget Y', 149.99, 5, 'Gadgets');
            INSERT INTO Products VALUES ('p5', 'Tool Alpha', 35.00, 75, 'Tools');
            INSERT INTO Products VALUES ('p6', 'Tool Beta', 45.00, 30, 'Tools');
            INSERT INTO Products VALUES ('p7', 'Tool Gamma', 15.00, 200, 'Tools');
            INSERT INTO Products VALUES ('p8', 'Premium Widget', 500.00, 2, 'Electronics');
            """;
        seedCmd.ExecuteNonQuery();
    }

    [Fact]
    public void WhereGeneric_SimpleEquality_ReturnsMatchingRows()
    {
        var statement = "Products"
            .From()
            .SelectAll()
            .Where<TestProduct>(p => p.Category == "Tools")
            .ToSqlStatement();

        var sqlResult = statement.ToSQLite();
        var sqlOk = Assert.IsType<StringOk>(sqlResult);
        var sql = sqlOk.Value;
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

        if (
            queryResult
            is not Result<IReadOnlyList<TestProduct>, SqlError>.Ok<
                IReadOnlyList<TestProduct>,
                SqlError
            > ok
        )
        {
            Assert.Fail("Expected Ok result");
            return;
        }

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

        var sqlResult = statement.ToSQLite();
        var sqlOk = Assert.IsType<StringOk>(sqlResult);
        var sql = sqlOk.Value;
        Assert.Contains(">", sql);

        var queryResult = _connection.Query<(string Name, double Price)>(
            sql: sql,
            mapper: r => (r.GetString(0), r.GetDouble(1))
        );

        if (
            queryResult
            is not Result<IReadOnlyList<(string Name, double Price)>, SqlError>.Ok<
                IReadOnlyList<(string Name, double Price)>,
                SqlError
            > ok
        )
        {
            Assert.Fail("Expected Ok result");
            return;
        }

        var rows = ok.Value;
        Assert.Equal(3, rows.Count);
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

        var sqlResult = statement.ToSQLite();
        var sqlOk = Assert.IsType<StringOk>(sqlResult);
        var sql = sqlOk.Value;
        Assert.Contains("AND", sql, StringComparison.OrdinalIgnoreCase);

        var queryResult = _connection.Query<(string Name, double Price)>(
            sql: sql,
            mapper: r => (r.GetString(0), r.GetDouble(1))
        );

        if (
            queryResult
            is not Result<IReadOnlyList<(string Name, double Price)>, SqlError>.Ok<
                IReadOnlyList<(string Name, double Price)>,
                SqlError
            > ok
        )
        {
            Assert.Fail("Expected Ok result");
            return;
        }

        var rows = ok.Value;
        Assert.Equal(3, rows.Count);
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

        var sqlResult = statement.ToSQLite();
        var sqlOk = Assert.IsType<StringOk>(sqlResult);
        var sql = sqlOk.Value;
        Assert.Contains("OR", sql, StringComparison.OrdinalIgnoreCase);

        var queryResult = _connection.Query<(string Name, double Price)>(
            sql: sql,
            mapper: r => (r.GetString(0), r.GetDouble(1))
        );

        if (
            queryResult
            is not Result<IReadOnlyList<(string Name, double Price)>, SqlError>.Ok<
                IReadOnlyList<(string Name, double Price)>,
                SqlError
            > ok
        )
        {
            Assert.Fail("Expected Ok result");
            return;
        }

        var rows = ok.Value;
        Assert.Equal(4, rows.Count);
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

        var sqlResult = statement.ToSQLite();
        var sqlOk = Assert.IsType<StringOk>(sqlResult);
        var sql = sqlOk.Value;
        Assert.Contains("LIKE", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Widget", sql);
        Assert.Contains("Name", sql);
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

        var sqlResult = statement.ToSQLite();
        var sqlOk = Assert.IsType<StringOk>(sqlResult);
        var sql = sqlOk.Value;
        Assert.Contains("LIKE", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Tool", sql);
        Assert.Contains("Name", sql);
    }

    [Fact]
    public void SelectGeneric_SingleColumn_GeneratesCorrectSQL()
    {
        var statement = "Products".From().Select<TestProduct>(p => p.Name).ToSqlStatement();

        var sqlResult = statement.ToSQLite();
        var sqlOk = Assert.IsType<StringOk>(sqlResult);
        var sql = sqlOk.Value;
        Assert.Contains("Name", sql);
        Assert.DoesNotContain("*", sql);
        Assert.Contains("FROM Products", sql);

        var queryResult = _connection.Query<string>(sql: sql, mapper: r => r.GetString(0));

        if (
            queryResult
            is not Result<IReadOnlyList<string>, SqlError>.Ok<IReadOnlyList<string>, SqlError> ok
        )
        {
            Assert.Fail("Expected Ok result");
            return;
        }

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

        var sqlResult = statement.ToSQLite();
        var sqlOk = Assert.IsType<StringOk>(sqlResult);
        var sql = sqlOk.Value;
        Assert.Contains("ORDER BY", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Price", sql);

        var queryResult = _connection.Query<(string Name, double Price)>(
            sql: sql,
            mapper: r => (r.GetString(0), r.GetDouble(1))
        );

        if (
            queryResult
            is not Result<IReadOnlyList<(string Name, double Price)>, SqlError>.Ok<
                IReadOnlyList<(string Name, double Price)>,
                SqlError
            > ok
        )
        {
            Assert.Fail("Expected Ok result");
            return;
        }

        var rows = ok.Value;
        Assert.Equal(8, rows.Count);
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

        var sqlResult = statement.ToSQLite();
        var sqlOk = Assert.IsType<StringOk>(sqlResult);
        var sql = sqlOk.Value;
        Assert.Contains("WHERE", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ORDER BY", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("LIMIT", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("OFFSET", sql, StringComparison.OrdinalIgnoreCase);

        var queryResult = _connection.Query<(string Name, double Price)>(
            sql: sql,
            mapper: r => (r.GetString(0), r.GetDouble(1))
        );

        if (
            queryResult
            is not Result<IReadOnlyList<(string Name, double Price)>, SqlError>.Ok<
                IReadOnlyList<(string Name, double Price)>,
                SqlError
            > ok
        )
        {
            Assert.Fail("Expected Ok result");
            return;
        }

        var rows = ok.Value;
        Assert.Equal(3, rows.Count);

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
}
