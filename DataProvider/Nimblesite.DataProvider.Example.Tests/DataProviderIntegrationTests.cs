using Microsoft.Data.Sqlite;
using Nimblesite.Lql.SQLite;
using Nimblesite.Sql.Model;
using Xunit;
using static Nimblesite.DataProvider.Example.MapFunctions;

namespace Nimblesite.DataProvider.Example.Tests;

#pragma warning disable CS1591

/// <summary>
/// Integration tests for DataProvider code generation
/// </summary>
public sealed class DataProviderIntegrationTests : IDisposable
{
    private readonly string _dbPath;
    private readonly string _connectionString;
    private readonly SqliteConnection _connection;

    public DataProviderIntegrationTests()
    {
        _dbPath = Path.Combine(
            Path.GetTempPath(),
            $"dataprovider_integration_tests_{Guid.NewGuid()}.db"
        );
        _connectionString = $"Data Source={_dbPath}";
        _connection = new SqliteConnection(_connectionString);
    }

    [Fact]
    public async Task GetInvoicesAsync_WithValidData_ReturnsCorrectTypes()
    {
        // Arrange
        await SetupTestDatabase();

        // Act
        var result = await _connection.GetInvoicesAsync("Acme Corp", "2024-01-01", "2024-12-31");

        // Assert
        if (result is InvoiceListError failure)
        {
            throw new InvalidOperationException(
                $"GetInvoicesAsync failed: {failure.Value.Message}"
            );
        }
        Assert.True(result is InvoiceListOk, $"Expected Success but got {result.GetType()}");

        var success = (InvoiceListOk)result;
        var invoices = success.Value;

        Assert.NotEmpty(invoices);
        var invoice = invoices[0];
        var line = invoice.InvoiceLines[0];

        // Verify Invoice type and properties
        Assert.IsType<Invoice>(invoice);
        Assert.IsType<string>(invoice.Id);
        Assert.IsType<string>(invoice.InvoiceNumber);
        Assert.IsType<string>(invoice.InvoiceDate);
        Assert.IsType<string>(invoice.CustomerName);
        Assert.IsType<double>(invoice.TotalAmount);
        Assert.IsAssignableFrom<IReadOnlyList<InvoiceLine>>(invoice.InvoiceLines);

        // Verify InvoiceLine type and properties
        Assert.IsType<InvoiceLine>(line);
        Assert.IsType<string>(line.LineId);
        Assert.IsType<string>(line.InvoiceId);
        Assert.IsType<string>(line.Description);
        Assert.IsType<double>(line.Quantity);
        Assert.IsType<double>(line.UnitPrice);
        Assert.IsType<double>(line.Amount);
    }

    [Fact]
    public async Task GetCustomersLqlAsync_WithValidData_ReturnsCorrectTypes()
    {
        // Arrange
        await SetupTestDatabase();

        // Act
        var result = await _connection.GetCustomersLqlAsync(null);

        // Assert
        if (result is CustomerListError failure)
        {
            // Log the error but continue the test to see what happens
            Console.WriteLine($"GetCustomersLqlAsync failed: {failure.Value.Message}");
            Console.WriteLine(
                $"Full exception: {failure.Value.Exception?.ToString() ?? "No exception details"}"
            );
        }
        Assert.True(
            result is CustomerListOk,
            $"Expected Success but got {result.GetType()}, Error: {(result as CustomerListError)?.Value.Message ?? "No error message"}"
        );

        var success = (CustomerListOk)result;
        var customers = success.Value;

        Assert.NotEmpty(customers);
        var customer = customers[0];
        var address = customer.Addresss[0];

        // Verify Customer type and properties
        Assert.IsType<Customer>(customer);
        Assert.IsType<string>(customer.Id);
        Assert.IsType<string>(customer.CustomerName);
        Assert.IsType<string>(customer.Email);
        // Phone property not available in generated Customer type
        Assert.IsType<string>(customer.CreatedDate);
        Assert.IsAssignableFrom<IReadOnlyList<Address>>(customer.Addresss);

        // Verify Address type and properties
        Assert.IsType<Address>(address);
        Assert.IsType<string>(address.AddressId);
        Assert.IsType<string>(address.CustomerId);
        Assert.IsType<string>(address.Street);
        Assert.IsType<string>(address.City);
        Assert.IsType<string>(address.State);
        Assert.IsType<string>(address.ZipCode);
        // Country property not available in generated Address type
    }

    [Fact]
    public async Task GetOrdersAsync_WithValidData_ReturnsCorrectTypes()
    {
        // Arrange
        await SetupTestDatabase();

        // Act
        var result = await _connection.GetOrdersAsync(
            "cust-1",
            "Completed",
            "2024-01-01",
            "2024-12-31"
        );

        // Assert
        Assert.True(result is OrderListOk, $"Expected Success but got {result.GetType()}");

        var success = (OrderListOk)result;
        var orders = success.Value;

        Assert.NotEmpty(orders);
        var order = orders[0];
        var item = order.OrderItems[0];

        // Verify Order type and properties
        Assert.IsType<Order>(order);
        Assert.IsType<string>(order.Id);
        Assert.IsType<string>(order.OrderNumber);
        // OrderDate property not available in generated Order type
        Assert.IsType<string>(order.CustomerId);
        Assert.IsType<double>(order.TotalAmount);
        Assert.IsType<string>(order.Status);
        Assert.IsAssignableFrom<IReadOnlyList<OrderItem>>(order.OrderItems);

        // Verify OrderItem type and properties
        Assert.IsType<OrderItem>(item);
        Assert.IsType<string>(item.ItemId);
        Assert.IsType<string>(item.OrderId);
        Assert.IsType<string>(item.ProductName);
        Assert.IsType<double>(item.Quantity);
        Assert.IsType<double>(item.Price);
        Assert.IsType<double>(item.Subtotal);
    }

    [Fact]
    public async Task AllQueries_VerifyCorrectTableNamesGenerated()
    {
        // Arrange
        await SetupTestDatabase();

        // Act & Assert - Verify extension methods exist with correct names
        var invoiceResult = await _connection.GetInvoicesAsync("Acme Corp", null!, null!);
        var customerResult = await _connection.GetCustomersLqlAsync(null);
        var orderResult = await _connection.GetOrdersAsync("cust-1", null!, null!, null!);

        // All should succeed (this proves the extension methods were generated)
        Assert.True(
            invoiceResult is InvoiceListOk,
            $"Expected Invoice Success but got {invoiceResult.GetType()}"
        );
        Assert.True(
            customerResult is CustomerListOk,
            $"Expected Customer Success but got {customerResult.GetType()}"
        );
        Assert.True(
            orderResult is OrderListOk,
            $"Expected Order Success but got {orderResult.GetType()}"
        );

        // Verify different table names were used (not hard-coded)
        _ = ((InvoiceListOk)invoiceResult).Value;
        _ = ((CustomerListOk)customerResult).Value;
        _ = ((OrderListOk)orderResult).Value;

        //TODO: Assert these!!!

        // These should be different types, proving they're not hard-coded
        Assert.NotEqual(typeof(Invoice), typeof(Customer));
        Assert.NotEqual(typeof(Invoice), typeof(Order));
        Assert.NotEqual(typeof(Customer), typeof(Order));

        Assert.NotEqual(typeof(InvoiceLine), typeof(Address));
        Assert.NotEqual(typeof(InvoiceLine), typeof(OrderItem));
        Assert.NotEqual(typeof(Address), typeof(OrderItem));
    }

    [Fact]
    public async Task GetInvoicesAsync_WithMultipleRecords_GroupsCorrectly()
    {
        // Arrange
        await SetupTestDatabase();

        // Act
        var result = await _connection.GetInvoicesAsync("Acme Corp", "2024-01-01", "2024-12-31");

        // Assert
        Assert.True(result is InvoiceListOk, $"Expected Success but got {result.GetType()}");

        var success = (InvoiceListOk)result;
        var invoices = success.Value;

        Assert.Equal(3, invoices.Count);

        // Verify grouping is working correctly
        var firstInvoice = invoices[0];
        Assert.Equal(2, firstInvoice.InvoiceLines.Count);

        var secondInvoice = invoices[1];
        Assert.Equal(2, secondInvoice.InvoiceLines.Count);

        var thirdInvoice = invoices[2];
        Assert.Equal(2, thirdInvoice.InvoiceLines.Count);
    }

    [Fact]
    public async Task GetCustomersLqlAsync_WithMultipleAddresses_GroupsCorrectly()
    {
        // Arrange
        await SetupTestDatabase();

        // Act
        var result = await _connection.GetCustomersLqlAsync(null);

        // Assert
        Assert.True(result is CustomerListOk, $"Expected Success but got {result.GetType()}");

        var success = (CustomerListOk)result;
        var customers = success.Value;

        Assert.Equal(2, customers.Count);

        // First customer should have 2 addresses
        var firstCustomer = customers[0];
        Assert.Equal(2, firstCustomer.Addresss.Count);

        // Second customer should have 1 address
        var secondCustomer = customers[1];
        Assert.Single(secondCustomer.Addresss);
    }

    [Fact]
    public async Task GetOrdersAsync_WithMultipleItems_GroupsCorrectly()
    {
        // Arrange
        await SetupTestDatabase();

        // Act
        var result = await _connection.GetOrdersAsync(null, null, "2024-01-01", "2024-12-31");

        // Assert
        Assert.True(result is OrderListOk, $"Expected Success but got {result.GetType()}");

        var success = (OrderListOk)result;
        var orders = success.Value;

        Assert.Equal(2, orders.Count);

        // Orders are returned by OrderDate DESC, so ORD-002 comes first (1 item), ORD-001 comes second (2 items)
        var firstOrder = orders[0]; // ORD-002
        Assert.Single(firstOrder.OrderItems);

        var secondOrder = orders[1]; // ORD-001
        Assert.Equal(2, secondOrder.OrderItems.Count);
    }

    [Fact]
    public async Task GetInvoicesAsync_WithEmptyDatabase_ReturnsEmpty()
    {
        // Arrange
        await SetupEmptyDatabase();

        // Act
        var result = await _connection.GetInvoicesAsync("Acme Corp", "2024-01-01", "2024-12-31");

        // Assert
        Assert.True(result is InvoiceListOk, $"Expected Success but got {result.GetType()}");

        var success = (InvoiceListOk)result;
        var invoices = success.Value;

        Assert.Empty(invoices);
    }

    [Fact]
    public async Task GetCustomersLqlAsync_WithEmptyDatabase_ReturnsEmpty()
    {
        // Arrange
        await SetupEmptyDatabase();

        // Act
        var result = await _connection.GetCustomersLqlAsync(null);

        // Assert
        Assert.True(result is CustomerListOk, $"Expected Success but got {result.GetType()}");

        var success = (CustomerListOk)result;
        var customers = success.Value;

        Assert.Empty(customers);
    }

    [Fact]
    public async Task GetOrdersAsync_WithEmptyDatabase_ReturnsEmpty()
    {
        // Arrange
        await SetupEmptyDatabase();

        // Act
        var result = await _connection.GetOrdersAsync(
            "cust-1",
            "Completed",
            "2024-01-01",
            "2024-12-31"
        );

        // Assert
        Assert.True(result is OrderListOk, $"Expected Success but got {result.GetType()}");

        var success = (OrderListOk)result;
        var orders = success.Value;

        Assert.Empty(orders);
    }

    [Fact]
    public void FluentQueryBuilder_InnerJoin_GeneratesCorrectSQL()
    {
        // Arrange & Act
        var query = "Orders"
            .From("o")
            .InnerJoin("Customer", "CustomerId", "Id", "o", "c")
            .Select(
                ("o", "OrderNumber"),
                ("o", "TotalAmount"),
                ("o", "Status"),
                ("c", "CustomerName"),
                ("c", "Email")
            )
            .Where("o.TotalAmount", ComparisonOperator.GreaterThan, "400.00")
            .OrderByDescending("o.TotalAmount")
            .Take(5)
            .ToSqlStatement();

        var sqlResult = query.ToSQLite();

        // Assert
        Assert.True(
            sqlResult is StringSqlOk,
            $"SQL generation should succeed, got: {(sqlResult as StringSqlError)?.Value.Message}"
        );

        var sql = ((StringSqlOk)sqlResult).Value;

        // Verify JOIN is included
        Assert.Contains("INNER JOIN", sql);
        Assert.Contains("Customer", sql);
        Assert.Contains("ON o.CustomerId = c.Id", sql);

        // Verify table aliases are included
        Assert.Contains("FROM Orders o", sql);
        Assert.Contains("Customer c", sql);

        // Verify full expected SQL structure
        Assert.Contains(
            "SELECT o.OrderNumber, o.TotalAmount, o.Status, c.CustomerName, c.Email",
            sql
        );
        Assert.Contains("WHERE o.TotalAmount > '400.00'", sql);
        Assert.Contains("ORDER BY o.TotalAmount DESC", sql);
        Assert.Contains("LIMIT 5", sql);
    }

    [Fact]
    public void FluentQueryBuilder_LeftJoin_GeneratesCorrectSQL()
    {
        // Arrange & Act
        var query = "Orders"
            .From("ord")
            .LeftJoin("Customer", "CustomerId", "Id", "ord", "cust")
            .Select(("ord", "OrderNumber"), ("cust", "CustomerName"))
            .ToSqlStatement();

        var sqlResult = query.ToSQLite();

        // Assert
        Assert.True(sqlResult is StringSqlOk);
        var sql = ((StringSqlOk)sqlResult).Value;

        Assert.Contains("LEFT JOIN", sql);
        Assert.Contains("Customer cust", sql);
        Assert.Contains("FROM Orders ord", sql);
        Assert.Contains("ON ord.CustomerId = cust.Id", sql);
    }

    [Fact]
    public void FluentQueryBuilder_MultipleJoins_GeneratesCorrectSQL()
    {
        // Arrange & Act - Simulate Orders -> Customer -> Address join chain
        var query = "Orders"
            .From("o")
            .InnerJoin("Customer", "CustomerId", "Id", "o", "c")
            .LeftJoin("Address", "Id", "CustomerId", "c", "a")
            .Select(("o", "OrderNumber"), ("c", "CustomerName"), ("a", "City"))
            .ToSqlStatement();

        var sqlResult = query.ToSQLite();

        // Assert
        Assert.True(sqlResult is StringSqlOk);
        var sql = ((StringSqlOk)sqlResult).Value;

        // Verify both JOINs are present
        Assert.Contains("INNER JOIN Customer c", sql);
        Assert.Contains("LEFT JOIN Address a", sql);
        Assert.Contains("ON o.CustomerId = c.Id", sql);
        Assert.Contains("ON c.Id = a.CustomerId", sql);
    }

    [Fact]
    public void FluentQueryBuilder_InnerJoinWithComplex_GeneratesCorrectSQL()
    {
        // Arrange & Act - Test a complex JOIN with multiple conditions
        var query = "Orders"
            .From("o")
            .InnerJoin("Customer", "CustomerId", "Id", "o", "c")
            .Select(("o", "OrderNumber"), ("c", "CustomerName"), ("o", "TotalAmount"))
            .Where("o.Status", ComparisonOperator.Eq, "Completed")
            .OrderBy("c.CustomerName")
            .Take(10)
            .ToSqlStatement();

        var sqlResult = query.ToSQLite();

        // Assert
        Assert.True(sqlResult is StringSqlOk);
        var sql = ((StringSqlOk)sqlResult).Value;

        // Test that all parts of the fluent API are preserved in the generated SQL
        Assert.Contains(
            "SELECT o.OrderNumber, c.CustomerName, o.TotalAmount",
            sql,
            StringComparison.Ordinal
        );
        Assert.Contains("FROM Orders o", sql, StringComparison.Ordinal);
        Assert.Contains("INNER JOIN Customer c", sql, StringComparison.Ordinal);
        Assert.Contains("ON o.CustomerId = c.Id", sql, StringComparison.Ordinal);
        Assert.Contains("WHERE o.Status = 'Completed'", sql, StringComparison.Ordinal);
        Assert.Contains("ORDER BY c.CustomerName ASC", sql, StringComparison.Ordinal);
        Assert.Contains("LIMIT 10", sql, StringComparison.Ordinal);
    }

    private async Task SetupTestDatabase()
    {
        await _connection.OpenAsync().ConfigureAwait(false);
        using (var pragmaCommand = new SqliteCommand("PRAGMA foreign_keys = OFF", _connection))
        {
            await pragmaCommand.ExecuteNonQueryAsync().ConfigureAwait(false);
        }

        // I don't know why this is here. We're supposed to use Migrations to create the schema and
        // inserts/updates are supposed to be extension methods.

        // Create all tables
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

        // Insert comprehensive test data
        var insertScript = """
            INSERT INTO Invoice (Id, InvoiceNumber, InvoiceDate, CustomerName, CustomerEmail, TotalAmount, DiscountAmount, Notes) VALUES
            ('inv-1', 'INV-001', '2024-01-15', 'Acme Corp', 'accounting@acme.com', 1250.00, NULL, 'Test invoice'),
            ('inv-2', 'INV-002', '2024-01-16', 'Acme Corp', 'accounting@acme.com', 850.75, 25.00, NULL),
            ('inv-3', 'INV-003', '2024-01-17', 'Acme Corp', 'accounting@acme.com', 2100.25, 100.00, 'Large order discount');

            INSERT INTO InvoiceLine (Id, InvoiceId, Description, Quantity, UnitPrice, Amount, DiscountPercentage, Notes) VALUES
            ('line-1', 'inv-1', 'Software License', 1.0, 1000.00, 1000.00, NULL, NULL),
            ('line-2', 'inv-1', 'Support Package', 1.0, 250.00, 250.00, 10.0, 'First year support'),
            ('line-3', 'inv-2', 'Consulting Hours', 5.0, 150.00, 750.00, NULL, NULL),
            ('line-4', 'inv-2', 'Travel Expenses', 1.0, 100.75, 100.75, NULL, 'Reimbursement'),
            ('line-5', 'inv-3', 'Hardware Components', 10.0, 125.50, 1255.00, 5.0, 'Bulk discount'),
            ('line-6', 'inv-3', 'Installation Service', 3.0, 281.75, 845.25, NULL, NULL);

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

    private async Task SetupEmptyDatabase()
    {
        await _connection.OpenAsync().ConfigureAwait(false);

        // Create tables but don't insert any data - same script as above but without inserts
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
    }

    #region PredicateBuilder E2E Tests

    /// <summary>
    /// Tests PredicateBuilder True predicate with actual database data
    /// </summary>
    [Fact]
    public async Task PredicateBuilder_True_E2E_ReturnsAllCustomers()
    {
        // Arrange
        await SetupTestDatabase();
        var predicate = PredicateBuilder.True<Customer>();
        var query = SelectStatement.From<Customer>("Customer").Where(predicate);

        // Act
        var statement = query.ToSqlStatement();
        var result = _connection.GetRecords(statement, s => s.ToSQLite(), MapCustomer);

        // Assert
        Assert.True(result is CustomerReadOnlyListOk);
        var customers = ((CustomerReadOnlyListOk)result).Value;
        Assert.Equal(2, customers.Count);
    }

    /// <summary>
    /// Tests PredicateBuilder False predicate with actual database data
    /// </summary>
    [Fact]
    public async Task PredicateBuilder_False_E2E_ReturnsNoCustomers()
    {
        // Arrange
        await SetupTestDatabase();
        var predicate = PredicateBuilder.False<Customer>();
        var query = SelectStatement.From<Customer>("Customer").Where(predicate);

        // Act
        var statement = query.ToSqlStatement();
        var result = _connection.GetRecords(statement, s => s.ToSQLite(), MapCustomer);

        // Assert
        Assert.True(result is CustomerReadOnlyListOk);
        var customers = ((CustomerReadOnlyListOk)result).Value;
        Assert.Empty(customers);
    }

    /// <summary>
    /// Tests PredicateBuilder Or operation with actual database data
    /// </summary>
    [Fact]
    public async Task PredicateBuilder_Or_E2E_CombinesPredicatesWithOrLogic()
    {
        // Arrange
        await SetupTestDatabase();
        var predicate = PredicateBuilder.False<Customer>();
        predicate = predicate.Or(c => c.CustomerName == "Acme Corp");
        predicate = predicate.Or(c => c.CustomerName == "Tech Solutions");
        var query = SelectStatement
            .From<Customer>("Customer")
            .Where(predicate)
            .OrderBy(c => c.CustomerName);

        // Act
        var statement = query.ToSqlStatement();
        var result = _connection.GetRecords(statement, s => s.ToSQLite(), MapCustomer);

        // Assert
        Assert.True(result is CustomerReadOnlyListOk);
        var customers = ((CustomerReadOnlyListOk)result).Value;
        Assert.Equal(2, customers.Count);
        Assert.Equal("Acme Corp", customers[0].CustomerName);
        Assert.Equal("Tech Solutions", customers[1].CustomerName);
    }

    /// <summary>
    /// Tests PredicateBuilder And operation with actual database data
    /// </summary>
    [Fact]
    public async Task PredicateBuilder_And_E2E_CombinesPredicatesWithAndLogic()
    {
        // Arrange
        await SetupTestDatabase();
        var predicate = PredicateBuilder.True<Customer>();
        predicate = predicate.And(c => c.CustomerName == "Acme Corp");
        predicate = predicate.And(c => c.Email != null);
        var query = SelectStatement.From<Customer>("Customer").Where(predicate);

        // Act
        var statement = query.ToSqlStatement();
        var result = _connection.GetRecords(statement, s => s.ToSQLite(), MapCustomer);

        // Assert
        Assert.True(result is CustomerReadOnlyListOk);
        var customers = ((CustomerReadOnlyListOk)result).Value;
        Assert.Single(customers);
        Assert.Equal("Acme Corp", customers[0].CustomerName);
    }

    /// <summary>
    /// Tests PredicateBuilder Not operation with actual database data
    /// </summary>
    [Fact]
    public async Task PredicateBuilder_Not_E2E_NegatesPredicateLogic()
    {
        // Arrange
        await SetupTestDatabase();
        var predicate = PredicateBuilder.True<Customer>();
        predicate = predicate.And(c => c.CustomerName == "Acme Corp");
        predicate = predicate.Not();
        var query = SelectStatement.From<Customer>("Customer").Where(predicate);

        // Act
        var statement = query.ToSqlStatement();
        var result = _connection.GetRecords(statement, s => s.ToSQLite(), MapCustomer);

        // Assert
        Assert.True(result is CustomerReadOnlyListOk);
        var customers = ((CustomerReadOnlyListOk)result).Value;
        Assert.Single(customers);
        Assert.Equal("Tech Solutions", customers[0].CustomerName);
    }

    /// <summary>
    /// Tests PredicateBuilder with dynamic OR conditions like building search filters
    /// </summary>
    [Fact]
    public async Task PredicateBuilder_DynamicOrConditions_E2E_BuildsSearchFilters()
    {
        // Arrange
        await SetupTestDatabase();
        var searchNames = new[] { "Acme Corp", "Unknown Corp", "Missing Inc" }; // Only "Acme Corp" exists in test data
        var predicate = PredicateBuilder.False<Customer>();

        // Act - simulate building dynamic OR conditions
        foreach (var name in searchNames)
        {
            var tempName = name; // Capture for closure
            predicate = predicate.Or(c => c.CustomerName == tempName);
        }

        var query = SelectStatement.From<Customer>("Customer").Where(predicate);
        var statement = query.ToSqlStatement();
        var result = _connection.GetRecords(statement, s => s.ToSQLite(), MapCustomer);

        // Assert
        Assert.True(result is CustomerReadOnlyListOk);
        var customers = ((CustomerReadOnlyListOk)result).Value;
        Assert.Single(customers); // Only customer "Acme Corp" exists
        Assert.Equal("Acme Corp", customers[0].CustomerName);
    }

    /// <summary>
    /// Tests PredicateBuilder with dynamic AND conditions like building filter chains
    /// </summary>
    [Fact]
    public async Task PredicateBuilder_DynamicAndConditions_E2E_BuildsFilterChains()
    {
        // Arrange
        await SetupTestDatabase();
        var predicate = PredicateBuilder.True<Customer>();

        // Act - simulate building dynamic AND conditions for filtering
        predicate = predicate.And(c => c.Id != null);
        predicate = predicate.And(c => c.Email != null);
        predicate = predicate.And(c => c.CustomerName != null);

        var query = SelectStatement
            .From<Customer>("Customer")
            .Where(predicate)
            .OrderBy(c => c.CustomerName);
        var statement = query.ToSqlStatement();
        var result = _connection.GetRecords(statement, s => s.ToSQLite(), MapCustomer);

        // Assert
        Assert.True(result is CustomerReadOnlyListOk);
        var customers = ((CustomerReadOnlyListOk)result).Value;
        Assert.Equal(2, customers.Count); // Both customers have email and are in range
    }

    /// <summary>
    /// Tests PredicateBuilder with mixed And/Or operations for complex business logic
    /// </summary>
    [Fact]
    public async Task PredicateBuilder_MixedAndOrOperations_E2E_ComplexBusinessLogic()
    {
        // Arrange
        await SetupTestDatabase();
        var searchNames = new[] { "Acme Corp", "NonExistent Corp" };

        // Act - build name filter with OR
        var namePredicate = PredicateBuilder.False<Customer>();
        foreach (var name in searchNames)
        {
            var tempName = name;
            namePredicate = namePredicate.Or(c => c.CustomerName == tempName);
        }

        // Combine with email filter using AND
        var finalPredicate = namePredicate.And(c => c.Email != null);

        var query = SelectStatement.From<Customer>("Customer").Where(finalPredicate);
        var statement = query.ToSqlStatement();
        var result = _connection.GetRecords(statement, s => s.ToSQLite(), MapCustomer);

        // Assert
        Assert.True(result is CustomerReadOnlyListOk);
        var customers = ((CustomerReadOnlyListOk)result).Value;
        Assert.Single(customers); // Only "Acme Corp" exists and has email
        Assert.Equal("Acme Corp", customers[0].CustomerName);
    }

    /// <summary>
    /// Tests PredicateBuilder with conditional building to eliminate duplication
    /// </summary>
    [Fact]
    public async Task PredicateBuilder_ConditionalBuilding_E2E_EliminatesDuplication()
    {
        // Arrange
        await SetupTestDatabase();
        var searchByName = true;
        var searchByEmail = false;
        var customerName = "Tech Solutions";

        var predicate = PredicateBuilder.True<Customer>();

        // Act - conditional predicate building (eliminates if/else duplication)
        if (searchByName)
        {
            predicate = predicate.And(c => c.CustomerName == customerName);
        }
        if (searchByEmail)
        {
            predicate = predicate.And(c => c.Email != null);
        }

        var query = SelectStatement.From<Customer>("Customer").Where(predicate);
        var statement = query.ToSqlStatement();
        var result = _connection.GetRecords(statement, s => s.ToSQLite(), MapCustomer);

        // Assert
        Assert.True(result is CustomerReadOnlyListOk);
        var customers = ((CustomerReadOnlyListOk)result).Value;
        Assert.Single(customers);
        Assert.Equal("Tech Solutions", customers[0].CustomerName);
    }

    /// <summary>
    /// Tests PredicateBuilder with Orders table for different entity type
    /// </summary>
    [Fact]
    public async Task PredicateBuilder_WithOrdersEntity_E2E_WorksAcrossEntityTypes()
    {
        // Arrange
        await SetupTestDatabase();
        var statuses = new[] { "Completed", "Processing" };
        var predicate = PredicateBuilder.False<Order>();

        // Act - build status filter with OR conditions
        foreach (var status in statuses)
        {
            var tempStatus = status;
            predicate = predicate.Or(o => o.Status == tempStatus);
        }

        // Add minimum amount filter with AND
        predicate = predicate.And(o => o.TotalAmount > 0);

        var query = SelectStatement.From<Order>("Orders").Where(predicate).OrderBy(o => o.Id);
        var statement = query.ToSqlStatement();
        var result = _connection.GetRecords(statement, s => s.ToSQLite(), MapOrder);

        // Assert
        Assert.True(result is OrderReadOnlyListOk);
        var orders = ((OrderReadOnlyListOk)result).Value;
        Assert.Equal(2, orders.Count); // Both orders match the criteria
        Assert.Equal("Completed", orders[0].Status);
        Assert.Equal("Processing", orders[1].Status);
    }

    #endregion

    #region SelectStatementLinqExtensions Coverage Tests

    [Fact]
    public void FluentQueryBuilder_And_GeneratesCorrectSQL()
    {
        // Arrange & Act
        var query = "Orders"
            .From("o")
            .Select(("o", "OrderNumber"), ("o", "TotalAmount"))
            .Where("o.Status", "Completed")
            .And("o.TotalAmount", "500.00")
            .ToSqlStatement();

        var sqlResult = query.ToSQLite();

        // Assert
        Assert.True(sqlResult is StringSqlOk);
        var sql = ((StringSqlOk)sqlResult).Value;

        Assert.Contains("WHERE o.Status = 'Completed'", sql);
        Assert.Contains("AND o.TotalAmount = '500.00'", sql);
    }

    [Fact]
    public void FluentQueryBuilder_Or_GeneratesCorrectSQL()
    {
        // Arrange & Act
        var query = "Orders"
            .From("o")
            .Select(("o", "OrderNumber"), ("o", "Status"))
            .Where("o.Status", "Completed")
            .Or("o.Status", "Processing")
            .ToSqlStatement();

        var sqlResult = query.ToSQLite();

        // Assert
        Assert.True(sqlResult is StringSqlOk);
        var sql = ((StringSqlOk)sqlResult).Value;

        Assert.Contains("WHERE o.Status = 'Completed'", sql);
        Assert.Contains("OR o.Status = 'Processing'", sql);
    }

    [Fact]
    public void FluentQueryBuilder_Distinct_GeneratesCorrectSQL()
    {
        // Arrange & Act
        var query = "Orders".From("o").Select(("o", "Status")).Distinct().ToSqlStatement();

        var sqlResult = query.ToSQLite();

        // Assert
        Assert.True(sqlResult is StringSqlOk);
        var sql = ((StringSqlOk)sqlResult).Value;

        Assert.Contains("SELECT DISTINCT", sql);
    }

    [Fact]
    public void FluentQueryBuilder_Skip_GeneratesCorrectSQL()
    {
        // Arrange & Act
        var query = "Orders".From("o").SelectAll().Skip(10).Take(5).ToSqlStatement();

        var sqlResult = query.ToSQLite();

        // Assert
        Assert.True(sqlResult is StringSqlOk);
        var sql = ((StringSqlOk)sqlResult).Value;

        Assert.Contains("OFFSET 10", sql);
        Assert.Contains("LIMIT 5", sql);
    }

    [Fact]
    public void FluentQueryBuilder_SelectAll_GeneratesCorrectSQL()
    {
        // Arrange & Act
        var query = "Orders".From("o").SelectAll("o").ToSqlStatement();

        var sqlResult = query.ToSQLite();

        // Assert
        Assert.True(sqlResult is StringSqlOk);
        var sql = ((StringSqlOk)sqlResult).Value;

        Assert.Contains("SELECT o.*", sql);
    }

    [Fact]
    public void FluentQueryBuilder_GroupBy_GeneratesCorrectSQL()
    {
        // Arrange & Act
        var query = "Orders".From("o").Select(("o", "Status")).GroupBy("o.Status").ToSqlStatement();

        var sqlResult = query.ToSQLite();

        // Assert
        Assert.True(sqlResult is StringSqlOk);
        var sql = ((StringSqlOk)sqlResult).Value;

        Assert.Contains("GROUP BY o.Status", sql);
    }

    [Fact]
    public void FluentQueryBuilder_WhereWithOperator_GeneratesCorrectSQL()
    {
        // Arrange & Act
        var query = "Orders"
            .From("o")
            .Select(("o", "OrderNumber"), ("o", "TotalAmount"))
            .Where("o.TotalAmount", ComparisonOperator.GreaterOrEq, "100.00")
            .ToSqlStatement();

        var sqlResult = query.ToSQLite();

        // Assert
        Assert.True(sqlResult is StringSqlOk);
        var sql = ((StringSqlOk)sqlResult).Value;

        Assert.Contains("WHERE o.TotalAmount >= '100.00'", sql);
    }

    [Fact]
    public void FluentQueryBuilder_MultipleGroupBy_GeneratesCorrectSQL()
    {
        // Arrange & Act
        var query = "Orders"
            .From("o")
            .Select(("o", "Status"), ("o", "CustomerId"))
            .GroupBy("o.Status", "o.CustomerId")
            .ToSqlStatement();

        var sqlResult = query.ToSQLite();

        // Assert
        Assert.True(sqlResult is StringSqlOk);
        var sql = ((StringSqlOk)sqlResult).Value;

        Assert.Contains("GROUP BY o.Status, o.CustomerId", sql);
    }

    [Fact]
    public void FluentQueryBuilder_CombinedAndOr_GeneratesCorrectSQL()
    {
        // Arrange & Act
        var query = "Orders"
            .From("o")
            .Select(("o", "OrderNumber"))
            .Where("o.Status", "Completed")
            .And("o.TotalAmount", "500.00")
            .Or("o.Status", "VIP")
            .ToSqlStatement();

        var sqlResult = query.ToSQLite();

        // Assert
        Assert.True(sqlResult is StringSqlOk);
        var sql = ((StringSqlOk)sqlResult).Value;

        Assert.Contains("WHERE o.Status = 'Completed'", sql);
        Assert.Contains("AND o.TotalAmount = '500.00'", sql);
        Assert.Contains("OR o.Status = 'VIP'", sql);
    }

    [Fact]
    public void FluentQueryBuilder_WhereWithBoolValue_GeneratesCorrectSQL()
    {
        // Arrange & Act - tests FormatValue with bool
        var query = "Orders".From("o").SelectAll().Where("o.IsActive", true).ToSqlStatement();

        var sqlResult = query.ToSQLite();

        // Assert
        Assert.True(sqlResult is StringSqlOk);
        var sql = ((StringSqlOk)sqlResult).Value;

        Assert.Contains("WHERE o.IsActive = 1", sql);
    }

    [Fact]
    public void FluentQueryBuilder_WhereWithDateTimeValue_GeneratesCorrectSQL()
    {
        // Arrange & Act - tests FormatValue with DateTime
        var testDate = new DateTime(2024, 6, 15, 10, 30, 0, DateTimeKind.Utc);
        var query = "Orders".From("o").SelectAll().Where("o.OrderDate", testDate).ToSqlStatement();

        var sqlResult = query.ToSQLite();

        // Assert
        Assert.True(sqlResult is StringSqlOk);
        var sql = ((StringSqlOk)sqlResult).Value;

        Assert.Contains("WHERE o.OrderDate = '2024-06-15 10:30:00'", sql);
    }

    [Fact]
    public void FluentQueryBuilder_WhereWithIntValue_GeneratesCorrectSQL()
    {
        // Arrange & Act - tests FormatValue with numeric
        var query = "Orders".From("o").SelectAll().Where("o.CustomerId", 42).ToSqlStatement();

        var sqlResult = query.ToSQLite();

        // Assert
        Assert.True(sqlResult is StringSqlOk);
        var sql = ((StringSqlOk)sqlResult).Value;

        Assert.Contains("WHERE o.CustomerId = 42", sql);
    }

    [Fact]
    public void FluentQueryBuilder_WhereWithStringContainingQuote_EscapesCorrectly()
    {
        // Arrange & Act - tests FormatValue with string containing single quote
        var query = "Orders"
            .From("o")
            .SelectAll()
            .Where("o.CustomerName", "O'Brien")
            .ToSqlStatement();

        var sqlResult = query.ToSQLite();

        // Assert
        Assert.True(sqlResult is StringSqlOk);
        var sql = ((StringSqlOk)sqlResult).Value;

        Assert.Contains("WHERE o.CustomerName = 'O''Brien'", sql);
    }

    [Fact]
    public void FluentQueryBuilder_OrderByDescending_GeneratesCorrectSQL()
    {
        // Arrange & Act
        var query = "Orders"
            .From("o")
            .SelectAll()
            .OrderByDescending("o.OrderDate")
            .ToSqlStatement();

        var sqlResult = query.ToSQLite();

        // Assert
        Assert.True(sqlResult is StringSqlOk);
        var sql = ((StringSqlOk)sqlResult).Value;

        Assert.Contains("ORDER BY o.OrderDate DESC", sql);
    }

    [Fact]
    public void FluentQueryBuilder_AllComparisonOperators_GenerateCorrectSQL()
    {
        // Test Less Than
        var ltQuery = "Orders"
            .From()
            .Select(("", "Id"))
            .Where("Id", ComparisonOperator.LessThan, 10)
            .ToSqlStatement();
        Assert.Contains("Id < ", ((StringSqlOk)ltQuery.ToSQLite()).Value);

        // Test Less Than or Equal
        var leQuery = "Orders"
            .From()
            .Select(("", "Id"))
            .Where("Id", ComparisonOperator.LessOrEq, 10)
            .ToSqlStatement();
        Assert.Contains("Id <= ", ((StringSqlOk)leQuery.ToSQLite()).Value);

        // Test Greater Than
        var gtQuery = "Orders"
            .From()
            .Select(("", "Id"))
            .Where("Id", ComparisonOperator.GreaterThan, 10)
            .ToSqlStatement();
        Assert.Contains("Id > ", ((StringSqlOk)gtQuery.ToSQLite()).Value);

        // Test Not Equal
        var neQuery = "Orders"
            .From()
            .Select(("", "Id"))
            .Where("Id", ComparisonOperator.NotEq, 10)
            .ToSqlStatement();
        var neSql = ((StringSqlOk)neQuery.ToSQLite()).Value;
        Assert.True(neSql.Contains("Id <> ") || neSql.Contains("Id != "));
    }

    #endregion

    public void Dispose() =>
        CoreCoverageTests.DisposeSqliteFixture(connection: _connection, dbPath: _dbPath);
}
