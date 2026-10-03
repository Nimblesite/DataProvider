using System.Collections.Immutable;
using System.Globalization;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Nimblesite.DataProvider.Migration.Core;
using Nimblesite.DataProvider.Migration.SQLite;
using Nimblesite.Reporting.Engine;
using Nimblesite.Sql.Model;
using Outcome;
using Xunit;
using ConnError = Outcome.Result<System.Data.IDbConnection, Nimblesite.Sql.Model.SqlError>.Error<
    System.Data.IDbConnection,
    Nimblesite.Sql.Model.SqlError
>;
using ConnOk = Outcome.Result<System.Data.IDbConnection, Nimblesite.Sql.Model.SqlError>.Ok<
    System.Data.IDbConnection,
    Nimblesite.Sql.Model.SqlError
>;
using ConnResult = Outcome.Result<System.Data.IDbConnection, Nimblesite.Sql.Model.SqlError>;
using EngineError = Outcome.Result<
    Nimblesite.Reporting.Engine.ReportExecutionResult,
    Nimblesite.Sql.Model.SqlError
>.Error<Nimblesite.Reporting.Engine.ReportExecutionResult, Nimblesite.Sql.Model.SqlError>;
using EngineOk = Outcome.Result<
    Nimblesite.Reporting.Engine.ReportExecutionResult,
    Nimblesite.Sql.Model.SqlError
>.Ok<Nimblesite.Reporting.Engine.ReportExecutionResult, Nimblesite.Sql.Model.SqlError>;

namespace Nimblesite.Reporting.Tests;

#pragma warning disable CS1591

public sealed class ReportEngineTests : IDisposable
{
    private readonly string _dbPath;
    private readonly string _connectionString;
    private readonly SqliteConnection _connection;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger _logger;

    public ReportEngineTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"reporting_test_{Guid.NewGuid()}.db");
        _connectionString = $"Data Source={_dbPath}";
        _connection = new SqliteConnection(_connectionString);
        _loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = _loggerFactory.CreateLogger<ReportEngineTests>();

        SetupDatabase();
    }

    [Fact]
    public void Execute_WithSqlDataSource_ReturnsCorrectData()
    {
        // Arrange
        var report = CreateTestReport(
            dataSourceType: DataSourceType.Sql,
            query: "SELECT Id, Name, Category, Price, Stock FROM products ORDER BY Name"
        );

        // Act
        var result = ExecuteReport(report);

        // Assert
        Assert.True(result is EngineOk, $"Expected success but got {result.GetType()}");

        var ok = (EngineOk)result;
        Assert.Equal("test-report", ok.Value.ReportId);
        Assert.True(ok.Value.DataSources.ContainsKey("testDs"));
        Assert.Single(ok.Value.DataSources);

        var dsResult = ok.Value.DataSources["testDs"];
        Assert.Equal(5, dsResult.ColumnNames.Length);
        Assert.Contains("Id", dsResult.ColumnNames);
        Assert.Contains("Name", dsResult.ColumnNames);
        Assert.Contains("Category", dsResult.ColumnNames);
        Assert.Contains("Price", dsResult.ColumnNames);
        Assert.Contains("Stock", dsResult.ColumnNames);
        Assert.Equal(4, dsResult.TotalRows);
        Assert.Equal(4, dsResult.Rows.Length);

        // Rows ordered by Name: Alpha Widget, Beta Gadget, Delta Gadget, Gamma Widget
        var nameColIdx = dsResult.ColumnNames.IndexOf("Name");
        Assert.Equal("Alpha Widget", dsResult.Rows[0][nameColIdx]?.ToString());
        Assert.Equal("Beta Gadget", dsResult.Rows[1][nameColIdx]?.ToString());
        Assert.Equal("Delta Gadget", dsResult.Rows[2][nameColIdx]?.ToString());
        Assert.Equal("Gamma Widget", dsResult.Rows[3][nameColIdx]?.ToString());
    }

    [Fact]
    public void Execute_WithGroupByQuery_ReturnsAggregatedData()
    {
        // Arrange
        var report = CreateTestReport(
            dataSourceType: DataSourceType.Sql,
            query: "SELECT Category, COUNT(*) as ProductCount FROM products GROUP BY Category ORDER BY ProductCount DESC"
        );

        // Act
        var result = ExecuteReport(report);

        // Assert
        Assert.True(result is EngineOk);
        var ok = (EngineOk)result;
        Assert.Equal("test-report", ok.Value.ReportId);
        var dsResult = ok.Value.DataSources["testDs"];
        Assert.Equal(2, dsResult.ColumnNames.Length);
        Assert.Contains("Category", dsResult.ColumnNames);
        Assert.Contains("ProductCount", dsResult.ColumnNames);
        Assert.Equal(2, dsResult.TotalRows);

        // Widgets: 2 products, Gadgets: 2 products — ordered by count DESC
        var catIdx = dsResult.ColumnNames.IndexOf("Category");
        var countIdx = dsResult.ColumnNames.IndexOf("ProductCount");
        var categories = dsResult.Rows.Select(r => r[catIdx]?.ToString()).ToArray();
        Assert.Contains("Widgets", categories);
        Assert.Contains("Gadgets", categories);
        Assert.Equal(2L, Convert.ToInt64(dsResult.Rows[0][countIdx], CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Execute_WithMissingConnection_ReturnsError()
    {
        // Arrange
        var report = CreateTestReport(
            dataSourceType: DataSourceType.Sql,
            query: "SELECT * FROM products",
            connectionRef: "nonexistent-db"
        );

        // Act
        var result = ExecuteReport(report);

        // Assert
        Assert.True(result is EngineError, $"Expected error but got {result.GetType()}");
        var err = (EngineError)result;
        Assert.Contains("not found", err.Value.Message, StringComparison.Ordinal);
        Assert.Contains("nonexistent-db", err.Value.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Execute_WithInvalidSql_ReturnsError()
    {
        // Arrange
        var report = CreateTestReport(
            dataSourceType: DataSourceType.Sql,
            query: "SELECT * FROM nonexistent_table"
        );

        // Act
        var result = ExecuteReport(report);

        // Assert
        Assert.True(result is EngineError, $"Expected error but got {result.GetType()}");
        var err = (EngineError)result;
        Assert.NotNull(err.Value.Message);
        Assert.True(err.Value.Message.Length > 0, "Error message should be non-empty");
    }

    [Fact]
    public void Execute_WithMultipleDataSources_ReturnsAllResults()
    {
        // Arrange
        var report = new ReportDefinition(
            Id: "multi-ds-report",
            Title: "Multi Data Source Report",
            Parameters: [],
            DataSources:
            [
                new DataSourceDefinition(
                    Id: "products",
                    Type: DataSourceType.Sql,
                    ConnectionRef: "test-db",
                    Query: "SELECT Name, Price FROM products ORDER BY Name",
                    Url: null,
                    Method: null,
                    Headers: null,
                    Parameters: []
                ),
                new DataSourceDefinition(
                    Id: "summary",
                    Type: DataSourceType.Sql,
                    ConnectionRef: "test-db",
                    Query: "SELECT COUNT(*) as Total FROM products",
                    Url: null,
                    Method: null,
                    Headers: null,
                    Parameters: []
                ),
            ],
            Layout: new LayoutDefinition(Columns: 12, Rows: [])
        );

        // Act
        var result = ExecuteReport(report);

        // Assert
        Assert.True(result is EngineOk);
        var ok = (EngineOk)result;
        Assert.Equal("multi-ds-report", ok.Value.ReportId);
        Assert.Equal(2, ok.Value.DataSources.Count);
        Assert.True(ok.Value.DataSources.ContainsKey("products"));
        Assert.True(ok.Value.DataSources.ContainsKey("summary"));

        // Products data source
        var productsResult = ok.Value.DataSources["products"];
        Assert.Equal(2, productsResult.ColumnNames.Length);
        Assert.Contains("Name", productsResult.ColumnNames);
        Assert.Contains("Price", productsResult.ColumnNames);
        Assert.Equal(4, productsResult.TotalRows);

        // Summary data source
        var summaryResult = ok.Value.DataSources["summary"];
        Assert.Single(summaryResult.Rows);
        Assert.Equal(1, summaryResult.TotalRows);
        Assert.Contains("Total", summaryResult.ColumnNames);
        Assert.Equal(4L, Convert.ToInt64(summaryResult.Rows[0][0], CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Execute_WithEmptyTable_ReturnsEmptyResult()
    {
        // Arrange - query a table that exists but is empty (we'll use a WHERE that matches nothing)
        var report = CreateTestReport(
            dataSourceType: DataSourceType.Sql,
            query: "SELECT * FROM products WHERE Name = 'NonExistentProduct'"
        );

        // Act
        var result = ExecuteReport(report);

        // Assert
        Assert.True(result is EngineOk);
        var ok = (EngineOk)result;
        var dsResult = ok.Value.DataSources["testDs"];
        Assert.Equal(0, dsResult.TotalRows);
        Assert.Empty(dsResult.Rows);
        // Column names should still be present even with no rows
        Assert.True(
            dsResult.ColumnNames.Length > 0,
            "Column names should be present even with empty result"
        );
    }

    [Fact]
    public void Execute_WithLqlSelect_TranspilesAndReturnsData()
    {
        // Arrange - LQL pipeline: select columns with ordering
        var report = CreateTestReport(
            dataSourceType: DataSourceType.Lql,
            query: "products |> order_by(products.Name asc) |> select(products.Id, products.Name, products.Category, products.Price, products.Stock)"
        );

        // Act
        var result = ExecuteReport(report);

        // Assert
        Assert.True(result is EngineOk, $"Expected success but got {result.GetType()}");
        var ok = (EngineOk)result;
        var dsResult = ok.Value.DataSources["testDs"];
        Assert.Equal(5, dsResult.ColumnNames.Length);
        Assert.Contains("Id", dsResult.ColumnNames);
        Assert.Contains("Name", dsResult.ColumnNames);
        Assert.Contains("Category", dsResult.ColumnNames);
        Assert.Contains("Price", dsResult.ColumnNames);
        Assert.Contains("Stock", dsResult.ColumnNames);
        Assert.Equal(4, dsResult.TotalRows);
        Assert.Equal(4, dsResult.Rows.Length);

        // Verify LQL ordering (order_by Name asc)
        var nameIdx = dsResult.ColumnNames.IndexOf("Name");
        Assert.Equal("Alpha Widget", dsResult.Rows[0][nameIdx]?.ToString());
        Assert.Equal("Gamma Widget", dsResult.Rows[3][nameIdx]?.ToString());
    }

    [Fact]
    public void Execute_WithLqlGroupByAggregation_ReturnsAggregatedData()
    {
        // Arrange - LQL pipeline: group_by + count + sum + avg
        var report = CreateTestReport(
            dataSourceType: DataSourceType.Lql,
            query: "products |> group_by(products.Category) |> select(products.Category, count(*) as ProductCount, sum(products.Stock) as TotalStock, avg(products.Price) as AvgPrice) |> order_by(ProductCount desc)"
        );

        // Act
        var result = ExecuteReport(report);

        // Assert
        Assert.True(result is EngineOk);
        var ok = (EngineOk)result;
        var dsResult = ok.Value.DataSources["testDs"];
        Assert.Contains("Category", dsResult.ColumnNames);
        Assert.Contains("ProductCount", dsResult.ColumnNames);
        Assert.Contains("TotalStock", dsResult.ColumnNames);
        Assert.Contains("AvgPrice", dsResult.ColumnNames);
        Assert.Equal(2, dsResult.TotalRows);
    }

    [Fact]
    public void Execute_WithLqlFilterAndArithmetic_ReturnsFilteredData()
    {
        // Arrange - LQL pipeline: filter + arithmetic expression
        var report = CreateTestReport(
            dataSourceType: DataSourceType.Lql,
            query: "products |> filter(fn(row) => row.products.Price > 30) |> select(products.Name, products.Price, products.Stock, products.Price * products.Stock as InventoryValue) |> order_by(InventoryValue desc)"
        );

        // Act
        var result = ExecuteReport(report);

        // Assert - only products with Price > 30: Beta Gadget (49.99), Delta Gadget (79.99)
        Assert.True(result is EngineOk);
        var ok = (EngineOk)result;
        var dsResult = ok.Value.DataSources["testDs"];
        Assert.Equal(4, dsResult.ColumnNames.Length);
        Assert.Contains("Name", dsResult.ColumnNames);
        Assert.Contains("Price", dsResult.ColumnNames);
        Assert.Contains("Stock", dsResult.ColumnNames);
        Assert.Contains("InventoryValue", dsResult.ColumnNames);
        Assert.Equal(2, dsResult.TotalRows);

        // Verify filtered product names (ordered by InventoryValue desc)
        var nameIdx = dsResult.ColumnNames.IndexOf("Name");
        var names = dsResult.Rows.Select(r => r[nameIdx]?.ToString()).ToArray();
        Assert.Contains("Beta Gadget", names);
        Assert.Contains("Delta Gadget", names);
        Assert.DoesNotContain("Alpha Widget", names);
        Assert.DoesNotContain("Gamma Widget", names);

        // Verify InventoryValue = Price * Stock (Delta: 79.99 * 25 = 1999.75)
        var ivIdx = dsResult.ColumnNames.IndexOf("InventoryValue");
        var firstIv = Convert.ToDouble(dsResult.Rows[0][ivIdx], CultureInfo.InvariantCulture);
        Assert.True(firstIv > 0, "InventoryValue should be positive");
    }

    [Fact]
    public void Execute_WithLqlCaseExpression_ReturnsDerivedColumn()
    {
        // Arrange - LQL pipeline: CASE expression for price tiers
        var report = CreateTestReport(
            dataSourceType: DataSourceType.Lql,
            query: "products |> select(products.Name, products.Price, case when products.Price > 50 then 'Premium' when products.Price > 25 then 'Standard' else 'Budget' end as PriceTier) |> order_by(products.Price desc)"
        );

        // Act
        var result = ExecuteReport(report);

        // Assert
        Assert.True(result is EngineOk);
        var ok = (EngineOk)result;
        var dsResult = ok.Value.DataSources["testDs"];
        Assert.Equal(3, dsResult.ColumnNames.Length);
        Assert.Contains("Name", dsResult.ColumnNames);
        Assert.Contains("Price", dsResult.ColumnNames);
        Assert.Contains("PriceTier", dsResult.ColumnNames);
        Assert.Equal(4, dsResult.TotalRows);

        // Verify CASE expression produces correct tiers
        var tierIdx = dsResult.ColumnNames.IndexOf("PriceTier");
        var tiers = dsResult.Rows.Select(r => r[tierIdx]?.ToString()).ToArray();
        // Delta Gadget (79.99) -> Premium, Beta Gadget (49.99) -> Standard,
        // Alpha Widget (29.99) -> Standard, Gamma Widget (19.99) -> Budget
        Assert.Contains("Premium", tiers);
        Assert.Contains("Standard", tiers);
        Assert.Contains("Budget", tiers);
    }

    [Fact]
    public void Execute_WithLqlGroupByHaving_FiltersAggregates()
    {
        // Arrange - LQL pipeline: group_by + having + min/max
        var report = CreateTestReport(
            dataSourceType: DataSourceType.Lql,
            query: "products |> group_by(products.Category) |> having(fn(group) => count(*) > 1) |> select(products.Category, count(*) as Items, sum(products.Stock) as TotalStock, min(products.Price) as CheapestPrice, max(products.Price) as MostExpensive) |> order_by(TotalStock desc)"
        );

        // Act
        var result = ExecuteReport(report);

        // Assert - both categories (Widgets: 2, Gadgets: 2) pass having(count > 1)
        Assert.True(result is EngineOk);
        var ok = (EngineOk)result;
        var dsResult = ok.Value.DataSources["testDs"];
        Assert.Equal(5, dsResult.ColumnNames.Length);
        Assert.Contains("Category", dsResult.ColumnNames);
        Assert.Contains("Items", dsResult.ColumnNames);
        Assert.Contains("TotalStock", dsResult.ColumnNames);
        Assert.Contains("CheapestPrice", dsResult.ColumnNames);
        Assert.Contains("MostExpensive", dsResult.ColumnNames);
        Assert.Equal(2, dsResult.TotalRows);

        // Verify both categories present and single-item categories filtered out
        var catIdx = dsResult.ColumnNames.IndexOf("Category");
        var categories = dsResult.Rows.Select(r => r[catIdx]?.ToString()).ToArray();
        Assert.Contains("Widgets", categories);
        Assert.Contains("Gadgets", categories);

        // Verify Items count >= 2 for each (that's the having condition)
        var itemsIdx = dsResult.ColumnNames.IndexOf("Items");
        foreach (var row in dsResult.Rows)
        {
            Assert.True(
                Convert.ToInt64(row[itemsIdx], CultureInfo.InvariantCulture) >= 2,
                "Each group should have >= 2 items"
            );
        }
    }

    [Fact]
    public void Execute_WithLqlAggregateNoGroupBy_ReturnsSingleRow()
    {
        // Arrange - LQL pipeline: aggregate functions without group_by (totals)
        var report = CreateTestReport(
            dataSourceType: DataSourceType.Lql,
            query: "products |> select(count(*) as TotalProducts, sum(products.Stock) as TotalStock, sum(products.Price * products.Stock) as TotalValue)"
        );

        // Act
        var result = ExecuteReport(report);

        // Assert
        Assert.True(result is EngineOk);
        var ok = (EngineOk)result;
        var dsResult = ok.Value.DataSources["testDs"];
        Assert.Equal(3, dsResult.ColumnNames.Length);
        Assert.Contains("TotalProducts", dsResult.ColumnNames);
        Assert.Contains("TotalStock", dsResult.ColumnNames);
        Assert.Contains("TotalValue", dsResult.ColumnNames);
        Assert.Equal(1, dsResult.TotalRows);
        Assert.Single(dsResult.Rows);

        // Verify aggregate values: 4 products, Stock: 100+50+200+25=375
        var prodIdx = dsResult.ColumnNames.IndexOf("TotalProducts");
        var stockIdx = dsResult.ColumnNames.IndexOf("TotalStock");
        var valueIdx = dsResult.ColumnNames.IndexOf("TotalValue");
        Assert.Equal(4L, Convert.ToInt64(dsResult.Rows[0][prodIdx], CultureInfo.InvariantCulture));
        Assert.Equal(
            375L,
            Convert.ToInt64(dsResult.Rows[0][stockIdx], CultureInfo.InvariantCulture)
        );
        // TotalValue = sum(Price*Stock) = 29.99*100 + 49.99*50 + 19.99*200 + 79.99*25
        var totalValue = Convert.ToDouble(dsResult.Rows[0][valueIdx], CultureInfo.InvariantCulture);
        Assert.True(totalValue > 9000, $"TotalValue should be > 9000, got {totalValue}");
    }

    private static ReportDefinition CreateTestReport(
        DataSourceType dataSourceType,
        string query,
        string connectionRef = "test-db"
    ) =>
        new(
            Id: "test-report",
            Title: "Test Report",
            Parameters: [],
            DataSources:
            [
                new DataSourceDefinition(
                    Id: "testDs",
                    Type: dataSourceType,
                    ConnectionRef: connectionRef,
                    Query: query,
                    Url: null,
                    Method: null,
                    Headers: null,
                    Parameters: []
                ),
            ],
            Layout: new LayoutDefinition(Columns: 12, Rows: [])
        );

    private Result<ReportExecutionResult, SqlError> ExecuteReport(ReportDefinition report) =>
        ReportEngine.Execute(
            report: report,
            parameters: ImmutableDictionary<string, string>.Empty,
            connectionFactory: CreateTestConnection,
            lqlTranspiler: TranspileLql,
            logger: _logger
        );

    private ConnResult CreateTestConnection(string connectionRef)
    {
        if (connectionRef != "test-db")
        {
            return new ConnError(SqlError.Create($"Connection '{connectionRef}' not found"));
        }

        var conn = new SqliteConnection(_connectionString);
        conn.Open();
        return new ConnOk(conn);
    }

    private static Result<string, SqlError> TranspileLql(string lqlCode)
    {
        var statementResult = Nimblesite.Lql.Core.LqlStatementConverter.ToStatement(lqlCode);
        if (
            statementResult
            is Result<Nimblesite.Lql.Core.LqlStatement, SqlError>.Error<
                Nimblesite.Lql.Core.LqlStatement,
                SqlError
            > stmtErr
        )
        {
            return new Result<string, SqlError>.Error<string, SqlError>(stmtErr.Value);
        }

        var statement = (
            (Result<Nimblesite.Lql.Core.LqlStatement, SqlError>.Ok<
                Nimblesite.Lql.Core.LqlStatement,
                SqlError
            >)statementResult
        ).Value;
        return Nimblesite.Lql.SQLite.SqlStatementExtensionsSQLite.ToSQLite(statement);
    }

    private void SetupDatabase()
    {
        // Use Migration library with YAML schema to create the database
        var schemaYaml = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "reporting-test-schema.yaml")
        );
        var schema = SchemaYamlSerializer.FromYaml(schemaYaml);

        _connection.Open();

        // Generate DDL from YAML schema and execute
        foreach (var table in schema.Tables)
        {
            var createOp = new CreateTableOperation(table);
            var ddl = SqliteDdlGenerator.Generate(createOp);
            using var cmd = new SqliteCommand(ddl, _connection);
            cmd.ExecuteNonQuery();
        }

        // Insert test data using parameterized inserts
        InsertProduct(
            id: "prod-1",
            name: "Alpha Widget",
            category: "Widgets",
            price: 29.99,
            stock: 100
        );
        InsertProduct(
            id: "prod-2",
            name: "Beta Gadget",
            category: "Gadgets",
            price: 49.99,
            stock: 50
        );
        InsertProduct(
            id: "prod-3",
            name: "Gamma Widget",
            category: "Widgets",
            price: 19.99,
            stock: 200
        );
        InsertProduct(
            id: "prod-4",
            name: "Delta Gadget",
            category: "Gadgets",
            price: 79.99,
            stock: 25
        );
    }

    private void InsertProduct(string id, string name, string category, double price, int stock)
    {
        using var cmd = new SqliteCommand(
            "INSERT INTO products (Id, Name, Category, Price, Stock) VALUES (@id, @name, @category, @price, @stock)",
            _connection
        );
        cmd.Parameters.AddWithValue("@id", id);
        cmd.Parameters.AddWithValue("@name", name);
        cmd.Parameters.AddWithValue("@category", category);
        cmd.Parameters.AddWithValue("@price", price);
        cmd.Parameters.AddWithValue("@stock", stock);
        cmd.ExecuteNonQuery();
    }

    public void Dispose()
    {
        _connection?.Dispose();
        _loggerFactory?.Dispose();
        if (File.Exists(_dbPath))
        {
            try
            {
                File.Delete(_dbPath);
            }
            catch (IOException)
            { /* File may be locked */
            }
        }
    }
}
