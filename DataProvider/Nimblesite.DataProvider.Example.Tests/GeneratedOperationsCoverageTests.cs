using System.Data;
using Microsoft.Data.Sqlite;
using Nimblesite.Sql.Model;
using Outcome;
using Xunit;
using AffectedRowsOk = Outcome.Result<int, Nimblesite.Sql.Model.SqlError>.Ok<
    int,
    Nimblesite.Sql.Model.SqlError
>;
using AffectedRowsResult = Outcome.Result<int, Nimblesite.Sql.Model.SqlError>;

namespace Nimblesite.DataProvider.Example.Tests;

#pragma warning disable CS1591

/// <summary>
/// Tests for generated Insert/Update operations and SampleDataSeeder
/// </summary>
public sealed partial class GeneratedOperationsCoverageTests : IDisposable
{
    private readonly string _dbPath;
    private readonly SqliteConnection _connection;

    public GeneratedOperationsCoverageTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"generated_ops_tests_{Guid.NewGuid()}.db");
        _connection = new SqliteConnection($"Data Source={_dbPath}");
    }

    [Fact]
    public async Task SampleDataSeeder_SeedDataAsync_InsertsAllEntities()
    {
        await SetupSchema().ConfigureAwait(false);

        var (flowControl, result) = await _connection
            .Transact(async tx => await SampleDataSeeder.SeedDataAsync(tx).ConfigureAwait(false))
            .ConfigureAwait(false);

        Assert.True(flowControl);
        Assert.True(result is StringSqlOk);
    }

    [Fact]
    public async Task InsertCustomerAsync_WithValidData_ReturnsOk()
    {
        await AssertAffectedRows(
                execute: tx =>
                    tx.InsertCustomerAsync(
                        Guid.NewGuid().ToString(),
                        "Test Customer",
                        "test@test.com",
                        "555-1234",
                        "2024-01-01"
                    ),
                expectedRows: 1
            )
            .ConfigureAwait(false);
    }

    [Fact]
    public async Task InsertInvoiceAsync_WithValidData_ReturnsOk()
    {
        await AssertAffectedRows(
                execute: tx =>
                    tx.InsertInvoiceAsync(
                        Guid.NewGuid().ToString(),
                        "INV-TEST-001",
                        "2024-06-01",
                        "Test Corp",
                        "billing@test.com",
                        1500.00,
                        null,
                        "Test invoice"
                    ),
                expectedRows: 1
            )
            .ConfigureAwait(false);
    }

    [Fact]
    public async Task InsertInvoiceLineAsync_WithValidData_ReturnsOk()
    {
        await SetupSchemaAndSeed().ConfigureAwait(false);

        await _connection
            .Transact(async tx =>
            {
                var queryResult = tx.Query(
                    sql: "SELECT Id FROM Invoice LIMIT 1",
                    mapper: reader => reader.GetString(0)
                );
                var invoices = (Result<IReadOnlyList<string>, SqlError>.Ok<
                    IReadOnlyList<string>,
                    SqlError
                >)queryResult;
                var invoiceId = invoices.Value[0];

                var result = await tx.InsertInvoiceLineAsync(
                        Guid.NewGuid().ToString(),
                        invoiceId,
                        "Test Line Item",
                        2.0,
                        75.00,
                        150.00,
                        5.0,
                        "Test notes"
                    )
                    .ConfigureAwait(false);
                Assert.Equal(1, Assert.IsType<AffectedRowsOk>(result).Value);
            })
            .ConfigureAwait(false);
    }

    [Fact]
    public async Task InsertAddressAsync_WithValidData_ReturnsOk()
    {
        await SetupSchemaAndSeed().ConfigureAwait(false);

        await _connection
            .Transact(async tx =>
            {
                var queryResult = tx.Query(
                    sql: "SELECT Id FROM Customer LIMIT 1",
                    mapper: reader => reader.GetString(0)
                );
                var customers = (Result<IReadOnlyList<string>, SqlError>.Ok<
                    IReadOnlyList<string>,
                    SqlError
                >)queryResult;

                var result = await tx.InsertAddressAsync(
                        Guid.NewGuid().ToString(),
                        customers.Value[0],
                        "100 Test St",
                        "TestCity",
                        "TS",
                        "12345",
                        "USA"
                    )
                    .ConfigureAwait(false);
                Assert.Equal(1, Assert.IsType<AffectedRowsOk>(result).Value);
            })
            .ConfigureAwait(false);
    }

    [Fact]
    public async Task InsertOrdersAsync_WithValidData_ReturnsOk()
    {
        await SetupSchemaAndSeed().ConfigureAwait(false);

        await _connection
            .Transact(async tx =>
            {
                var queryResult = tx.Query(
                    sql: "SELECT Id FROM Customer LIMIT 1",
                    mapper: reader => reader.GetString(0)
                );
                var customers = (Result<IReadOnlyList<string>, SqlError>.Ok<
                    IReadOnlyList<string>,
                    SqlError
                >)queryResult;

                var result = await tx.InsertOrdersAsync(
                        Guid.NewGuid().ToString(),
                        "ORD-TEST-001",
                        "2024-06-01",
                        customers.Value[0],
                        999.99,
                        "Pending"
                    )
                    .ConfigureAwait(false);
                Assert.Equal(1, Assert.IsType<AffectedRowsOk>(result).Value);
            })
            .ConfigureAwait(false);
    }

    [Fact]
    public async Task InsertOrderItemAsync_WithValidData_ReturnsOk()
    {
        await SetupSchemaAndSeed().ConfigureAwait(false);

        await _connection
            .Transact(async tx =>
            {
                var queryResult = tx.Query(
                    sql: "SELECT Id FROM Orders LIMIT 1",
                    mapper: reader => reader.GetString(0)
                );
                var orders = (Result<IReadOnlyList<string>, SqlError>.Ok<
                    IReadOnlyList<string>,
                    SqlError
                >)queryResult;

                var result = await tx.InsertOrderItemAsync(
                        Guid.NewGuid().ToString(),
                        orders.Value[0],
                        "Test Widget",
                        5.0,
                        25.00,
                        125.00
                    )
                    .ConfigureAwait(false);
                Assert.Equal(1, Assert.IsType<AffectedRowsOk>(result).Value);
            })
            .ConfigureAwait(false);
    }

    private async Task SetupSchema()
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
    }

    // Implements [CI-DESLOP]: retain the result type and row-count assertions
    // for each generated operation while sharing schema and transaction setup.
    private async Task AssertAffectedRows(
        Func<IDbTransaction, Task<AffectedRowsResult>> execute,
        int expectedRows,
        bool seed = false
    )
    {
        await (seed ? SetupSchemaAndSeed() : SetupSchema()).ConfigureAwait(false);
        await _connection
            .Transact(async transaction =>
            {
                var result = await execute(transaction).ConfigureAwait(false);
                Assert.Equal(expectedRows, Assert.IsType<AffectedRowsOk>(result).Value);
            })
            .ConfigureAwait(false);
    }

    private async Task SetupSchemaAndSeed()
    {
        await SetupSchema().ConfigureAwait(false);

        var insertScript = """
            INSERT INTO Customer (Id, CustomerName, Email, Phone, CreatedDate) VALUES
            ('cust-1', 'Acme Corp', 'contact@acme.com', '555-0100', '2024-01-01');
            INSERT INTO Invoice (Id, InvoiceNumber, InvoiceDate, CustomerName, CustomerEmail, TotalAmount, DiscountAmount, Notes) VALUES
            ('inv-1', 'INV-001', '2024-01-15', 'Acme Corp', 'accounting@acme.com', 1250.00, NULL, 'Test');
            INSERT INTO InvoiceLine (Id, InvoiceId, Description, Quantity, UnitPrice, Amount, DiscountPercentage, Notes) VALUES
            ('line-1', 'inv-1', 'Software License', 1.0, 1000.00, 1000.00, NULL, NULL);
            INSERT INTO Address (Id, CustomerId, Street, City, State, ZipCode, Country) VALUES
            ('addr-1', 'cust-1', '123 Business Ave', 'New York', 'NY', '10001', 'USA');
            INSERT INTO Orders (Id, OrderNumber, OrderDate, CustomerId, TotalAmount, Status) VALUES
            ('ord-1', 'ORD-001', '2024-01-10', 'cust-1', 500.00, 'Completed');
            INSERT INTO OrderItem (Id, OrderId, ProductName, Quantity, Price, Subtotal) VALUES
            ('item-1', 'ord-1', 'Widget A', 2.0, 100.00, 200.00);
            """;

        using var insertCommand = new SqliteCommand(insertScript, _connection);
        await insertCommand.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    public void Dispose() =>
        CoreCoverageTests.DisposeSqliteFixture(connection: _connection, dbPath: _dbPath);
}
