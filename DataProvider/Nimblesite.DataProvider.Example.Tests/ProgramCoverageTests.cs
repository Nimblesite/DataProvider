using Microsoft.Data.Sqlite;
using Nimblesite.Lql.SQLite;
using Nimblesite.Sql.Model;
using Xunit;
using static Nimblesite.DataProvider.Example.MapFunctions;

namespace Nimblesite.DataProvider.Example.Tests;

#pragma warning disable CS1591

/// <summary>
/// Tests for Program.cs demo methods and MapFunctions coverage
/// </summary>
public sealed class ProgramCoverageTests : IDisposable
{
    private readonly string _dbPath;
    private readonly SqliteConnection _connection;

    public ProgramCoverageTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"program_coverage_tests_{Guid.NewGuid()}.db");
        _connection = new SqliteConnection($"Data Source={_dbPath}");
    }

    [Fact]
    public async Task TestGeneratedQueriesAsync_WithValidData_Succeeds()
    {
        await SetupTestDatabase();
        await Program.TestGeneratedQueriesAsync(_connection);
    }

    [Fact]
    public async Task TestInvoiceQueryAsync_WithValidData_Succeeds()
    {
        await SetupTestDatabase();
        await Program.TestInvoiceQueryAsync(_connection);
    }

    [Fact]
    public async Task TestCustomerQueryAsync_WithValidData_Succeeds()
    {
        await SetupTestDatabase();
        await Program.TestCustomerQueryAsync(_connection);
    }

    [Fact]
    public async Task TestOrderQueryAsync_WithValidData_Succeeds()
    {
        await SetupTestDatabase();
        await Program.TestOrderQueryAsync(_connection);
    }

    [Fact]
    public async Task DemonstrateAdvancedQueryBuilding_WithValidData_Succeeds()
    {
        await SetupTestDatabase();
        Program.DemonstrateAdvancedQueryBuilding(_connection);
    }

    [Fact]
    public async Task DemoLinqQuerySyntax_WithValidData_LoadsCustomers()
    {
        await SetupTestDatabase();
        Program.DemoLinqQuerySyntax(_connection);
    }

    [Fact]
    public async Task DemoFluentQueryBuilder_WithValidData_LoadsHighValueOrders()
    {
        await SetupTestDatabase();
        Program.DemoFluentQueryBuilder(_connection);
    }

    [Fact]
    public async Task DemoLinqMethodSyntax_WithValidData_LoadsRecentOrders()
    {
        await SetupTestDatabase();
        Program.DemoLinqMethodSyntax(_connection);
    }

    [Fact]
    public void DemoComplexAggregation_Succeeds()
    {
        Program.DemoComplexAggregation();
    }

    [Fact]
    public async Task DemoComplexFiltering_WithValidData_LoadsPremiumOrders()
    {
        await SetupTestDatabase();
        Program.DemoComplexFiltering(_connection);
    }

    [Fact]
    public async Task DemonstratePredicateBuilder_WithValidData_Succeeds()
    {
        await SetupTestDatabase();
        Program.DemonstratePredicateBuilder(_connection);
    }

    [Fact]
    public void ShowcaseSummary_Succeeds()
    {
        Program.ShowcaseSummary();
    }

    [Fact]
    public async Task MapBasicOrder_WithJoinQuery_ReturnsCorrectData()
    {
        await SetupTestDatabase();

        var result = _connection.GetRecords(
            "Orders"
                .From("o")
                .InnerJoin("Customer", "CustomerId", "Id", "o", "c")
                .Select(
                    ("o", "OrderNumber"),
                    ("o", "TotalAmount"),
                    ("o", "Status"),
                    ("c", "CustomerName"),
                    ("c", "Email")
                )
                .OrderBy("o.OrderNumber")
                .ToSqlStatement(),
            stmt => stmt.ToSQLite(),
            MapBasicOrder
        );

        Assert.True(result is BasicOrderListOk, $"Expected success but got {result.GetType()}");
        var orders = ((BasicOrderListOk)result).Value;
        Assert.Equal(2, orders.Count);

        var firstOrder = orders[0];
        Assert.Equal("ORD-001", firstOrder.OrderNumber);
        Assert.Equal(500.00, firstOrder.TotalAmount);
        Assert.Equal("Completed", firstOrder.Status);
        Assert.Equal("Acme Corp", firstOrder.CustomerName);
        Assert.Equal("contact@acme.com", firstOrder.Email);

        var secondOrder = orders[1];
        Assert.Equal("ORD-002", secondOrder.OrderNumber);
        Assert.Equal(750.00, secondOrder.TotalAmount);
        Assert.Equal("Processing", secondOrder.Status);
        Assert.Equal("Tech Solutions", secondOrder.CustomerName);
        Assert.Equal("info@techsolutions.com", secondOrder.Email);
    }

    [Fact]
    public async Task MapBasicOrder_WithHighValueFilter_ReturnsFilteredResults()
    {
        await SetupTestDatabase();

        var result = _connection.GetRecords(
            "Orders"
                .From("o")
                .InnerJoin("Customer", "CustomerId", "Id", "o", "c")
                .Select(
                    ("o", "OrderNumber"),
                    ("o", "TotalAmount"),
                    ("o", "Status"),
                    ("c", "CustomerName"),
                    ("c", "Email")
                )
                .Where("o.TotalAmount", ComparisonOperator.GreaterThan, "600.00")
                .OrderByDescending("o.TotalAmount")
                .Take(5)
                .ToSqlStatement(),
            stmt => stmt.ToSQLite(),
            MapBasicOrder
        );

        Assert.True(result is BasicOrderListOk);
        var orders = ((BasicOrderListOk)result).Value;
        Assert.Single(orders);
        Assert.Equal("ORD-002", orders[0].OrderNumber);
        Assert.Equal(750.00, orders[0].TotalAmount);
    }

    [Fact]
    public async Task MapCustomer_WithNullEmail_ReturnsNullEmail()
    {
        await SetupTestDatabaseWithNullableFields();

        var result = _connection.GetRecords(
            (
                from customer in SelectStatement.From<Generated.Customer>()
                orderby customer.CustomerName
                select customer
            ).ToSqlStatement(),
            stmt => stmt.ToSQLite(),
            MapCustomer
        );

        Assert.True(
            result is CustomerReadOnlyListOk,
            $"Expected success but got {result.GetType()}"
        );
        var customers = ((CustomerReadOnlyListOk)result).Value;

        var nullEmailCustomer = customers.First(c => c.CustomerName == "No Email Corp");
        Assert.Null(nullEmailCustomer.Email);
        Assert.Equal("555-9999", nullEmailCustomer.Phone);
    }

    [Fact]
    public async Task MapCustomer_WithNullPhone_ReturnsNullPhone()
    {
        await SetupTestDatabaseWithNullableFields();

        var result = _connection.GetRecords(
            (
                from customer in SelectStatement.From<Generated.Customer>()
                orderby customer.CustomerName
                select customer
            ).ToSqlStatement(),
            stmt => stmt.ToSQLite(),
            MapCustomer
        );

        Assert.True(result is CustomerReadOnlyListOk);
        var customers = ((CustomerReadOnlyListOk)result).Value;

        var nullPhoneCustomer = customers.First(c => c.CustomerName == "No Phone Corp");
        Assert.Equal("nophone@test.com", nullPhoneCustomer.Email);
        Assert.Null(nullPhoneCustomer.Phone);
    }

    [Fact]
    public async Task MapCustomer_WithBothNullEmailAndPhone_ReturnsBothNull()
    {
        await SetupTestDatabaseWithNullableFields();

        var result = _connection.GetRecords(
            (
                from customer in SelectStatement.From<Generated.Customer>()
                orderby customer.CustomerName
                select customer
            ).ToSqlStatement(),
            stmt => stmt.ToSQLite(),
            MapCustomer
        );

        Assert.True(result is CustomerReadOnlyListOk);
        var customers = ((CustomerReadOnlyListOk)result).Value;

        var nullBothCustomer = customers.First(c => c.CustomerName == "No Contact Corp");
        Assert.Null(nullBothCustomer.Email);
        Assert.Null(nullBothCustomer.Phone);
    }

    [Fact]
    public async Task MapBasicOrder_WithLeftJoinAndComplexFilter_ReturnsCorrectResults()
    {
        await SetupTestDatabase();

        var result = _connection.GetRecords(
            "Orders"
                .From("o")
                .LeftJoin("Customer", "CustomerId", "Id", "o", "c")
                .Select(
                    ("o", "OrderNumber"),
                    ("o", "TotalAmount"),
                    ("o", "Status"),
                    ("c", "CustomerName"),
                    ("c", "Email")
                )
                .AddWhereCondition(WhereCondition.OpenParen())
                .Where("o.TotalAmount", ComparisonOperator.GreaterOrEq, "500.00")
                .AddWhereCondition(WhereCondition.And())
                .AddWhereCondition(
                    WhereCondition.Comparison(
                        ColumnInfo.Named("o.OrderDate"),
                        ComparisonOperator.GreaterOrEq,
                        "2024-01-01"
                    )
                )
                .AddWhereCondition(WhereCondition.CloseParen())
                .Or("o.Status", "VIP")
                .OrderBy("o.OrderDate")
                .Take(3)
                .ToSqlStatement(),
            stmt => stmt.ToSQLite(),
            MapBasicOrder
        );

        Assert.True(result is BasicOrderListOk);
        var orders = ((BasicOrderListOk)result).Value;
        Assert.NotEmpty(orders);

        foreach (var order in orders)
        {
            Assert.False(string.IsNullOrEmpty(order.OrderNumber));
            Assert.False(string.IsNullOrEmpty(order.CustomerName));
        }
    }

    [Fact]
    public async Task ProgramMain_RunsSuccessfully()
    {
        var originalDir = Environment.CurrentDirectory;
        var tempDir = Path.Combine(Path.GetTempPath(), $"program_main_test_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);
        try
        {
            Environment.CurrentDirectory = tempDir;
            await Program.Main([]).ConfigureAwait(false);
        }
        finally
        {
            Environment.CurrentDirectory = originalDir;
            try
            {
                Directory.Delete(tempDir, recursive: true);
            }
#pragma warning disable CA1031
            catch (IOException)
            { /* best effort */
            }
#pragma warning restore CA1031
        }
    }

    [Fact]
    public void ModelTypes_CanBeConstructed()
    {
        // Cover Example.Model record constructors
        var order = new Nimblesite.DataProvider.Example.Model.Order(
            Id: 1,
            OrderNumber: "ORD-001",
            OrderDate: "2024-01-01",
            CustomerId: 1,
            TotalAmount: 100.0,
            Status: "Completed",
            Items:
            [
                new Nimblesite.DataProvider.Example.Model.OrderItem(
                    Id: 1,
                    OrderId: 1,
                    ProductName: "Widget",
                    Quantity: 2.0,
                    Price: 50.0,
                    Subtotal: 100.0
                ),
            ]
        );

        Assert.Equal("ORD-001", order.OrderNumber);
        Assert.Single(order.Items);
        Assert.Equal("Widget", order.Items[0].ProductName);

        var customer = new Nimblesite.DataProvider.Example.Model.Customer(
            Id: 1,
            CustomerName: "Test Corp",
            Email: "test@test.com",
            Phone: "555-1234",
            CreatedDate: "2024-01-01",
            Addresses:
            [
                new Nimblesite.DataProvider.Example.Model.Address(
                    Id: 1,
                    CustomerId: 1,
                    Street: "123 Main St",
                    City: "TestCity",
                    State: "TS",
                    ZipCode: "12345",
                    Country: "USA"
                ),
            ]
        );

        Assert.Equal("Test Corp", customer.CustomerName);
        Assert.Single(customer.Addresses);
        Assert.Equal("TestCity", customer.Addresses[0].City);
    }

    [Fact]
    public void ModelRecords_EqualityWorks()
    {
        var order1 = new Nimblesite.DataProvider.Example.Model.Order(
            Id: 1,
            OrderNumber: "ORD-001",
            OrderDate: "2024-01-01",
            CustomerId: 1,
            TotalAmount: 100.0,
            Status: "Completed",
            Items: []
        );
        var order2 = new Nimblesite.DataProvider.Example.Model.Order(
            Id: 1,
            OrderNumber: "ORD-001",
            OrderDate: "2024-01-01",
            CustomerId: 1,
            TotalAmount: 100.0,
            Status: "Completed",
            Items: []
        );
        Assert.Equal(order1, order2);
        Assert.Equal(order1.GetHashCode(), order2.GetHashCode());
        Assert.Equal(order1.ToString(), order2.ToString());

        var item1 = new Nimblesite.DataProvider.Example.Model.OrderItem(
            Id: 1,
            OrderId: 1,
            ProductName: "W",
            Quantity: 1.0,
            Price: 10.0,
            Subtotal: 10.0
        );
        var item2 = new Nimblesite.DataProvider.Example.Model.OrderItem(
            Id: 1,
            OrderId: 1,
            ProductName: "W",
            Quantity: 1.0,
            Price: 10.0,
            Subtotal: 10.0
        );
        Assert.Equal(item1, item2);
        Assert.Equal(item1.GetHashCode(), item2.GetHashCode());

        var addr1 = new Nimblesite.DataProvider.Example.Model.Address(
            Id: 1,
            CustomerId: 1,
            Street: "St",
            City: "C",
            State: "S",
            ZipCode: "Z",
            Country: "US"
        );
        var addr2 = new Nimblesite.DataProvider.Example.Model.Address(
            Id: 1,
            CustomerId: 1,
            Street: "St",
            City: "C",
            State: "S",
            ZipCode: "Z",
            Country: "US"
        );
        Assert.Equal(addr1, addr2);
        Assert.Equal(addr1.GetHashCode(), addr2.GetHashCode());

        var cust1 = new Nimblesite.DataProvider.Example.Model.Customer(
            Id: 1,
            CustomerName: "Test",
            Email: null,
            Phone: null,
            CreatedDate: "2024-01-01",
            Addresses: []
        );
        var cust2 = new Nimblesite.DataProvider.Example.Model.Customer(
            Id: 1,
            CustomerName: "Test",
            Email: null,
            Phone: null,
            CreatedDate: "2024-01-01",
            Addresses: []
        );
        Assert.Equal(cust1, cust2);
        Assert.Equal(cust1.GetHashCode(), cust2.GetHashCode());

        var basic1 = new Nimblesite.DataProvider.Example.Model.BasicOrder(
            "ORD-001",
            100.0,
            "Completed",
            "Corp",
            "email@test.com"
        );
        var basic2 = new Nimblesite.DataProvider.Example.Model.BasicOrder(
            "ORD-001",
            100.0,
            "Completed",
            "Corp",
            "email@test.com"
        );
        Assert.Equal(basic1, basic2);
        Assert.Equal(basic1.GetHashCode(), basic2.GetHashCode());
    }

    private async Task SetupTestDatabase()
    {
        await _connection.OpenAsync().ConfigureAwait(false);
        using (var pragmaCommand = new SqliteCommand("PRAGMA foreign_keys = OFF", _connection))
        {
            await pragmaCommand.ExecuteNonQueryAsync().ConfigureAwait(false);
        }

        var createTablesScript = """
            CREATE TABLE IF NOT EXISTS Invoice (
                Id TEXT PRIMARY KEY,
                InvoiceNumber TEXT NOT NULL,
                InvoiceDate TEXT NOT NULL,
                CustomerName TEXT NOT NULL,
                CustomerEmail TEXT NULL,
                TotalAmount REAL NOT NULL,
                DiscountAmount REAL NULL,
                Notes TEXT NULL
            );
            CREATE TABLE IF NOT EXISTS InvoiceLine (
                Id TEXT PRIMARY KEY,
                InvoiceId TEXT NOT NULL,
                Description TEXT NOT NULL,
                Quantity REAL NOT NULL,
                UnitPrice REAL NOT NULL,
                Amount REAL NOT NULL,
                DiscountPercentage REAL NULL,
                Notes TEXT NULL,
                FOREIGN KEY (InvoiceId) REFERENCES Invoice (Id)
            );
            CREATE TABLE IF NOT EXISTS Customer (
                Id TEXT PRIMARY KEY,
                CustomerName TEXT NOT NULL,
                Email TEXT NULL,
                Phone TEXT NULL,
                CreatedDate TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS Address (
                Id TEXT PRIMARY KEY,
                CustomerId TEXT NOT NULL,
                Street TEXT NOT NULL,
                City TEXT NOT NULL,
                State TEXT NOT NULL,
                ZipCode TEXT NOT NULL,
                Country TEXT NOT NULL,
                FOREIGN KEY (CustomerId) REFERENCES Customer (Id)
            );
            CREATE TABLE IF NOT EXISTS Orders (
                Id TEXT PRIMARY KEY,
                OrderNumber TEXT NOT NULL,
                OrderDate TEXT NOT NULL,
                CustomerId TEXT NOT NULL,
                TotalAmount REAL NOT NULL,
                Status TEXT NOT NULL,
                FOREIGN KEY (CustomerId) REFERENCES Customer (Id)
            );
            CREATE TABLE IF NOT EXISTS OrderItem (
                Id TEXT PRIMARY KEY,
                OrderId TEXT NOT NULL,
                ProductName TEXT NOT NULL,
                Quantity REAL NOT NULL,
                Price REAL NOT NULL,
                Subtotal REAL NOT NULL,
                FOREIGN KEY (OrderId) REFERENCES Orders (Id)
            );
            """;

        using var command = new SqliteCommand(createTablesScript, _connection);
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);

        var insertScript = """
            INSERT INTO Invoice (Id, InvoiceNumber, InvoiceDate, CustomerName, CustomerEmail, TotalAmount, DiscountAmount, Notes) VALUES
            ('inv-1', 'INV-001', '2024-01-15', 'Acme Corp', 'accounting@acme.com', 1250.00, NULL, 'Test invoice');
            INSERT INTO InvoiceLine (Id, InvoiceId, Description, Quantity, UnitPrice, Amount, DiscountPercentage, Notes) VALUES
            ('line-1', 'inv-1', 'Software License', 1.0, 1000.00, 1000.00, NULL, NULL),
            ('line-2', 'inv-1', 'Support Package', 1.0, 250.00, 250.00, 10.0, 'First year support');
            INSERT INTO Customer (Id, CustomerName, Email, Phone, CreatedDate) VALUES
            ('cust-1', 'Acme Corp', 'contact@acme.com', '555-0100', '2024-01-01'),
            ('cust-2', 'Tech Solutions', 'info@techsolutions.com', '555-0200', '2024-01-02');
            INSERT INTO Address (Id, CustomerId, Street, City, State, ZipCode, Country) VALUES
            ('addr-1', 'cust-1', '123 Business Ave', 'New York', 'NY', '10001', 'USA'),
            ('addr-2', 'cust-1', '456 Main St', 'Albany', 'NY', '12201', 'USA'),
            ('addr-3', 'cust-2', '789 Tech Blvd', 'San Francisco', 'CA', '94105', 'USA');
            INSERT INTO Orders (Id, OrderNumber, OrderDate, CustomerId, TotalAmount, Status) VALUES
            ('ord-1', 'ORD-001', '2024-01-10', 'cust-1', 500.00, 'Completed'),
            ('ord-2', 'ORD-002', '2024-01-11', 'cust-2', 750.00, 'Processing');
            INSERT INTO OrderItem (Id, OrderId, ProductName, Quantity, Price, Subtotal) VALUES
            ('item-1', 'ord-1', 'Widget A', 2.0, 100.00, 200.00),
            ('item-2', 'ord-1', 'Widget B', 3.0, 100.00, 300.00),
            ('item-3', 'ord-2', 'Service Package', 1.0, 750.00, 750.00);
            """;

        using var insertCommand = new SqliteCommand(insertScript, _connection);
        await insertCommand.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    private async Task SetupTestDatabaseWithNullableFields()
    {
        await _connection.OpenAsync().ConfigureAwait(false);
        using (var pragmaCommand = new SqliteCommand("PRAGMA foreign_keys = OFF", _connection))
        {
            await pragmaCommand.ExecuteNonQueryAsync().ConfigureAwait(false);
        }

        var createTablesScript = """
            CREATE TABLE IF NOT EXISTS Customer (
                Id TEXT PRIMARY KEY,
                CustomerName TEXT NOT NULL,
                Email TEXT NULL,
                Phone TEXT NULL,
                CreatedDate TEXT NOT NULL
            );
            """;

        using var command = new SqliteCommand(createTablesScript, _connection);
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);

        var insertScript = """
            INSERT INTO Customer (Id, CustomerName, Email, Phone, CreatedDate) VALUES
            ('cust-1', 'Full Contact Corp', 'full@test.com', '555-1111', '2024-01-01'),
            ('cust-2', 'No Email Corp', NULL, '555-9999', '2024-01-02'),
            ('cust-3', 'No Phone Corp', 'nophone@test.com', NULL, '2024-01-03'),
            ('cust-4', 'No Contact Corp', NULL, NULL, '2024-01-04');
            """;

        using var insertCommand = new SqliteCommand(insertScript, _connection);
        await insertCommand.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    public void Dispose() =>
        CoreCoverageTests.DisposeSqliteFixture(connection: _connection, dbPath: _dbPath);
}
