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

        var createTablesScript = Data_SetupSchema_1;

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

        var insertScript = Data_SetupSchemaAndSeed_1;

        using var insertCommand = new SqliteCommand(insertScript, _connection);
        await insertCommand.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    public void Dispose() =>
        CoreCoverageTests.DisposeSqliteFixture(connection: _connection, dbPath: _dbPath);
}
