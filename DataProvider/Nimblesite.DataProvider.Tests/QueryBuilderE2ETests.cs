// Implements [CI-TEST-SHARED-COMMAND].
using System.Data;
using Nimblesite.Lql.SQLite;
using static global::Tests.Shared.ResultAssertions;
using static global::Tests.Shared.StringAssertions;

namespace Nimblesite.DataProvider.Tests;

/// <summary>
/// E2E tests: SelectStatement LINQ builder -> ToSQLite -> execute against real DB.
/// Also tests GetRecords API and PredicateBuilder workflows.
/// </summary>
public sealed partial class QueryBuilderE2ETests : IDisposable
{
    private readonly SqliteTestDatabase _db = new("qb_e2e");
    private SqliteConnection _connection => _db.Connection;

    internal SqliteConnection Connection => _connection;

    public QueryBuilderE2ETests()
    {
        CreateSchemaAndSeed();
    }

    public void Dispose() => _db.Dispose();

    private void CreateSchemaAndSeed() =>
        SqliteSchemaSetup.Apply(
            connection: _connection,
            command => command.CommandText = Data_CreateSchemaAndSeed_1,
            command => command.CommandText = Data_CreateSchemaAndSeed_2,
            command => command.CommandText = Data_CreateSchemaAndSeed_3,
            command => command.CommandText = Data_CreateSchemaAndSeed_4
        );

    [Fact]
    public void SelectStatementBuilder_WhereOrderByLimit_ExecutesCorrectly()
    {
        // Build query: SELECT Name, Price FROM Products WHERE Category = 'Tools' ORDER BY Price ASC LIMIT 2
        var statement = "Products"
            .From()
            .Select(columns: [(null, "Name"), (null, "Price")])
            .Where(columnName: "Category", value: "Tools")
            .OrderBy(columnName: "Price")
            .Take(count: 2)
            .ToSqlStatement();

        // Convert to SQLite
        var sqlResult = statement.ToSQLite();
        var sqlOk = global::Tests.Shared.ResultAssertions.ExactOk(actual: sqlResult);
        var sql = sqlOk.Value;
        VerifyReusableWorkflow1(input0: sql, value0: "SELECT", value2: "Products");
        Assert.Contains("LIMIT", sql, StringComparison.OrdinalIgnoreCase);

        // Execute against real DB
        var queryResult = _connection.Query<(string Name, double Price)>(
            sql: sql,
            mapper: r => (r.GetString(0), r.GetDouble(1))
        );
        var rows = ExactOk(actual: queryResult);
        Assert.Equal(2, rows.Value.Count);
        Assert.Equal("Tool Gamma", rows.Value[0].Name);
        Assert.Equal(15.00, rows.Value[0].Price);
        Assert.Equal("Tool Alpha", rows.Value[1].Name);
        Assert.Equal(35.00, rows.Value[1].Price);
    }

    [Fact]
    public void SelectStatementBuilder_DistinctAndGroupBy_ExecutesCorrectly()
    {
        // DISTINCT categories
        var distinctStmt = "Products"
            .From()
            .Select(columns: [(null, "Category")])
            .Distinct()
            .OrderBy(columnName: "Category")
            .ToSqlStatement();

        var distinctSql = VerifyConsolidatedPhase1(input0: distinctStmt);

        var categories = Ok(read: () =>
            _connection.Query<string>(sql: distinctSql, mapper: r => r.GetString(0))
        );
        Assert.Equal(3, categories.Value.Count);
        Assert.Equal("Electronics", categories.Value[0]);
        Assert.Equal("Gadgets", categories.Value[1]);
        Assert.Equal("Tools", categories.Value[2]);
    }

    [Fact]
    public void SelectStatementBuilder_WithPagination_ExecutesCorrectly()
    {
        // Page 2 (skip 3, take 3) ordered by name
        var pagedStmt = "Products"
            .From()
            .SelectAll()
            .OrderBy(columnName: "Name")
            .Skip(count: 3)
            .Take(count: 3)
            .ToSqlStatement();

        var pagedSql = VerifyConsolidatedPhase1(input0: pagedStmt);
        ContainsAll(
            actual: pagedSql,
            expected: ["OFFSET", "LIMIT"],
            comparison: StringComparison.OrdinalIgnoreCase
        );

        // Name is column index 1
        var page = Ok(read: () =>
            _connection.Query<string>(
                sql: pagedSql,
                mapper: r => r.GetString(1) // Name is column index 1
            )
        );
        Assert.Equal(3, page.Value.Count);
    }

    [Fact]
    public void SelectStatementBuilder_Join_ExecutesCorrectly()
    {
        // JOIN Products with Categories
        var joinStmt = "Products"
            .From()
            .Select(columns: [("Products", "Name"), ("Categories", "Description")])
            .InnerJoin(
                rightTable: "Categories",
                leftColumn: "Category",
                rightColumn: "Name",
                leftTableAlias: "Products",
                rightTableAlias: "Categories"
            )
            .Where(columnName: "Category", value: "Electronics")
            .OrderBy(columnName: "Products.Name")
            .ToSqlStatement();

        var joinSql = VerifyConsolidatedPhase1(input0: joinStmt);
        Assert.Contains("JOIN", joinSql, StringComparison.OrdinalIgnoreCase);

        var joinResult = _connection.Query<(string, string)>(
            sql: joinSql,
            mapper: r => (r.GetString(0), r.GetString(1))
        );
        var joined = ExactOk(actual: joinResult);
        Assert.Equal(3, joined.Value.Count);
        Assert.All(joined.Value, j => Assert.Equal("Electronic devices and components", j.Item2));
    }

    [Fact]
    public void GetRecords_WithSelectStatementAndSQLiteGenerator_MapsResultsCorrectly()
    {
        // Build a SelectStatement
        var statement = "Products"
            .From()
            .Select(columns: [(null, "Id"), (null, "Name"), (null, "Price")])
            .Where(columnName: "Price", ComparisonOperator.GreaterThan, 40.0)
            .OrderBy(columnName: "Price")
            .ToSqlStatement();

        // Use GetRecords with the SQLite generator using tuples
        var result = _connection.GetRecords<(string, string, double)>(
            statement: statement,
            sqlGenerator: stmt => stmt.ToSQLite(),
            mapper: r => (r.GetString(0), r.GetString(1), r.GetDouble(2))
        );

        var recordsOk = ExactOk(actual: result);
        Assert.Equal(4, recordsOk.Value.Count);
        Assert.Equal("Tool Beta", recordsOk.Value[0].Item2);
        Assert.Equal(45.00, recordsOk.Value[0].Item3);
        Assert.Equal("Premium Widget", recordsOk.Value[3].Item2);
        Assert.Equal(500.00, recordsOk.Value[3].Item3);
    }

    [Fact]
    public void GetRecords_NullGuards_ReturnErrors()
    {
        var statement = "Products".From().SelectAll().ToSqlStatement();

        // Null connection
        var nullConn = DbConnectionExtensions.GetRecords<string>(
            connection: null!,
            statement: statement,
            sqlGenerator: s => s.ToSQLite(),
            mapper: r => r.GetString(0)
        );
        _ = global::Tests.Shared.ResultAssertions.Error(actual: nullConn);

        // Null statement
        var nullStmt = _connection.GetRecords<string>(
            statement: null!,
            sqlGenerator: s => s.ToSQLite(),
            mapper: r => r.GetString(0)
        );
        _ = global::Tests.Shared.ResultAssertions.Error(actual: nullStmt);

        // Null generator
        var nullGen = _connection.GetRecords<string>(
            statement: statement,
            sqlGenerator: null!,
            mapper: r => r.GetString(0)
        );
        _ = global::Tests.Shared.ResultAssertions.Error(actual: nullGen);

        // Null mapper
        var nullMapper = _connection.GetRecords<string>(
            statement: statement,
            sqlGenerator: s => s.ToSQLite(),
            mapper: null!
        );
        _ = global::Tests.Shared.ResultAssertions.Error(actual: nullMapper);
    }

    [Fact]
    public void SelectStatementToSQLite_VariousStatements_GeneratesValidSQL()
    {
        // Simple select all
        var simple = "Products".From().SelectAll().ToSqlStatement();

        var simpleSql = SqlFragments(
            read: () => simple.ToSQLite(),
            expected: ["SELECT *", "FROM Products"]
        );

        // With specific columns
        var cols = "Products"
            .From()
            .Select(columns: [(null, "Name"), (null, "Price")])
            .ToSqlStatement();

        var colsSql = SqlFragments(read: () => cols.ToSQLite(), expected: ["Name", "Price"]);
        Assert.DoesNotContain("*", colsSql);

        // With WHERE + AND
        var filtered = "Products"
            .From()
            .SelectAll()
            .Where(columnName: "Category", value: "Tools")
            .And(columnName: "Price", value: 35.0)
            .ToSqlStatement();

        var filteredSql = SqlFragments(
            read: () => filtered.ToSQLite(),
            expected: ["WHERE", "AND"],
            comparison: StringComparison.OrdinalIgnoreCase
        );

        // Verify each generated SQL actually executes
        foreach (var sql in new[] { simpleSql, colsSql, filteredSql })
        {
            var result = _connection.Query<string>(sql: sql, mapper: r => r.GetString(0));
            Assert.IsNotType<Result<IReadOnlyList<string>, SqlError>.Error<
                IReadOnlyList<string>,
                SqlError
            >>(result);
        }
    }

    [Fact]
    public void SelectStatementBuilder_MultipleWhereConditions_GeneratesCorrectResults()
    {
        // OR condition: Electronics or Gadgets
        var orStmt = "Products"
            .From()
            .Select(columns: [(null, "Name"), (null, "Category")])
            .Where(columnName: "Category", value: "Electronics")
            .Or(columnName: "Category", value: "Gadgets")
            .OrderBy(columnName: "Name")
            .ToSqlStatement();

        var orSql = global::Tests.Shared.StringAssertions.SqlFragments(
            read: () => orStmt.ToSQLite(),
            expected: ["OR"],
            comparison: StringComparison.OrdinalIgnoreCase
        );

        var orRows = (
            Ok(read: () =>
                _connection.Query<(string Name, string Category)>(
                    sql: orSql,
                    mapper: r => (r.GetString(0), r.GetString(1))
                )
            )
        ).Value;
        Assert.Equal(5, orRows.Count);
        Assert.All(orRows, r => Assert.True(r.Category is "Electronics" or "Gadgets"));
    }

    [Fact]
    public void SelectStatementBuilder_ExpressionColumns_GeneratesCorrectSQL()
    {
        // Use expression column for computed values
        var builder = VerifyReusableWorkflow4(value0: "Products");
        builder.AddSelectColumn(name: "Name");
        builder.AddSelectColumn(
            ColumnInfo.FromExpression(expression: "Price * Quantity", alias: "TotalValue")
        );
        builder.AddOrderBy(column: "Name", direction: "ASC");
        var stmt = builder.Build();

        var sql = SqlFragments(
            read: () => stmt.ToSQLite(),
            expected: ["Price * Quantity", "TotalValue"]
        );

        var rows = (
            Ok(read: () =>
                _connection.Query<(string Name, double TotalValue)>(
                    sql: sql,
                    mapper: r => (r.GetString(0), r.GetDouble(1))
                )
            )
        ).Value;
        Assert.Equal(8, rows.Count);

        // Verify computed values
        var gadgetX = rows.First(r => r.Name == "Gadget X");
        Assert.Equal(999.90, gadgetX.TotalValue, precision: 2);

        var premiumWidget = rows.First(r => r.Name == "Premium Widget");
        Assert.Equal(1000.00, premiumWidget.TotalValue, precision: 2);
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    internal static string VerifyConsolidatedPhase1(SelectStatement input0)
    {
        var distinctSqlOk = global::Tests.Shared.ResultAssertions.ExactOk(
            actual: input0.ToSQLite()
        );
        var distinctSql = distinctSqlOk.Value;

        return distinctSql;
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    internal static void VerifyReusableWorkflow1(string input0, string value0, string value2)
    {
        Assert.Contains(value0, input0, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(value2, input0);
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    internal static SelectStatementBuilder VerifyReusableWorkflow4(string value0)
    { // Use expression column for computed values
        var builder = new SelectStatementBuilder();
        builder.AddTable(name: value0);

        return builder;
    }
}
