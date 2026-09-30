using Nimblesite.Sql.Model;
using Outcome;
using Xunit;
using AffectedRowsOk = Outcome.Result<int, Nimblesite.Sql.Model.SqlError>.Ok<
    int,
    Nimblesite.Sql.Model.SqlError
>;

namespace Nimblesite.DataProvider.Example.Tests;

/// <summary>Update operation coverage for the generated customer, invoice, address, and order methods.</summary>
public sealed partial class GeneratedOperationsCoverageTests
{
    /// <summary>Checks affected rows for UpdateCustomerAsync_WithValidData_ReturnsOk.</summary>
    [Fact]
    public async Task UpdateCustomerAsync_WithValidData_ReturnsOk()
    {
        await SetupSchemaAndSeed().ConfigureAwait(false);

        await _connection
            .Transact(async tx =>
            {
                // First get an existing customer ID
                var queryResult = tx.Query(
                    sql: "SELECT Id FROM Customer LIMIT 1",
                    mapper: reader => reader.GetString(0)
                );
                var customers = (Result<IReadOnlyList<string>, SqlError>.Ok<
                    IReadOnlyList<string>,
                    SqlError
                >)queryResult;
                var customerId = customers.Value[0];

                var result = await tx.UpdateCustomerAsync(
                        customerId,
                        "Updated Customer",
                        "updated@test.com",
                        "555-9999",
                        "2024-06-01"
                    )
                    .ConfigureAwait(false);
                Assert.Equal(1, Assert.IsType<AffectedRowsOk>(result).Value);
            })
            .ConfigureAwait(false);
    }

    /// <summary>Checks affected rows for UpdateInvoiceAsync_WithValidData_ReturnsOk.</summary>
    [Fact]
    public async Task UpdateInvoiceAsync_WithValidData_ReturnsOk()
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

                var result = await tx.UpdateInvoiceAsync(
                        invoiceId,
                        "INV-UPDATED",
                        "2024-07-01",
                        "Updated Corp",
                        "updated@billing.com",
                        2000.00,
                        100.00,
                        "Updated notes"
                    )
                    .ConfigureAwait(false);
                Assert.Equal(1, Assert.IsType<AffectedRowsOk>(result).Value);
            })
            .ConfigureAwait(false);
    }

    /// <summary>Checks affected rows for UpdateInvoiceLineAsync_WithValidData_ReturnsOk.</summary>
    [Fact]
    public async Task UpdateInvoiceLineAsync_WithValidData_ReturnsOk()
    {
        await SetupSchemaAndSeed().ConfigureAwait(false);

        await _connection
            .Transact(async tx =>
            {
                var queryResult = tx.Query(
                    sql: "SELECT Id, InvoiceId FROM InvoiceLine LIMIT 1",
                    mapper: reader => (Id: reader.GetString(0), InvoiceId: reader.GetString(1))
                );
                var lines = (Result<IReadOnlyList<(string Id, string InvoiceId)>, SqlError>.Ok<
                    IReadOnlyList<(string Id, string InvoiceId)>,
                    SqlError
                >)queryResult;
                var line = lines.Value[0];

                var result = await tx.UpdateInvoiceLineAsync(
                        line.Id,
                        line.InvoiceId,
                        "Updated Description",
                        3.0,
                        100.00,
                        300.00,
                        10.0,
                        "Updated notes"
                    )
                    .ConfigureAwait(false);
                Assert.Equal(1, Assert.IsType<AffectedRowsOk>(result).Value);
            })
            .ConfigureAwait(false);
    }

    /// <summary>Checks affected rows for UpdateAddressAsync_WithValidData_ReturnsOk.</summary>
    [Fact]
    public async Task UpdateAddressAsync_WithValidData_ReturnsOk()
    {
        await SetupSchemaAndSeed().ConfigureAwait(false);

        await _connection
            .Transact(async tx =>
            {
                var queryResult = tx.Query(
                    sql: "SELECT Id, CustomerId FROM Address LIMIT 1",
                    mapper: reader => (Id: reader.GetString(0), CustomerId: reader.GetString(1))
                );
                var addresses = (Result<IReadOnlyList<(string Id, string CustomerId)>, SqlError>.Ok<
                    IReadOnlyList<(string Id, string CustomerId)>,
                    SqlError
                >)queryResult;
                var addr = addresses.Value[0];

                var result = await tx.UpdateAddressAsync(
                        addr.Id,
                        addr.CustomerId,
                        "200 Updated Ave",
                        "UpdatedCity",
                        "UC",
                        "67890",
                        "USA"
                    )
                    .ConfigureAwait(false);
                Assert.Equal(1, Assert.IsType<AffectedRowsOk>(result).Value);
            })
            .ConfigureAwait(false);
    }

    /// <summary>Checks affected rows for UpdateOrdersAsync_WithValidData_ReturnsOk.</summary>
    [Fact]
    public async Task UpdateOrdersAsync_WithValidData_ReturnsOk()
    {
        await SetupSchemaAndSeed().ConfigureAwait(false);

        await _connection
            .Transact(async tx =>
            {
                var queryResult = tx.Query(
                    sql: "SELECT Id, CustomerId FROM Orders LIMIT 1",
                    mapper: reader => (Id: reader.GetString(0), CustomerId: reader.GetString(1))
                );
                var orders = (Result<IReadOnlyList<(string Id, string CustomerId)>, SqlError>.Ok<
                    IReadOnlyList<(string Id, string CustomerId)>,
                    SqlError
                >)queryResult;
                var order = orders.Value[0];

                var result = await tx.UpdateOrdersAsync(
                        order.Id,
                        "ORD-UPDATED",
                        "2024-07-01",
                        order.CustomerId,
                        1500.00,
                        "Completed"
                    )
                    .ConfigureAwait(false);
                Assert.Equal(1, Assert.IsType<AffectedRowsOk>(result).Value);
            })
            .ConfigureAwait(false);
    }

    /// <summary>Checks affected rows for UpdateOrderItemAsync_WithValidData_ReturnsOk.</summary>
    [Fact]
    public async Task UpdateOrderItemAsync_WithValidData_ReturnsOk()
    {
        await SetupSchemaAndSeed().ConfigureAwait(false);

        await _connection
            .Transact(async tx =>
            {
                var queryResult = tx.Query(
                    sql: "SELECT Id, OrderId FROM OrderItem LIMIT 1",
                    mapper: reader => (Id: reader.GetString(0), OrderId: reader.GetString(1))
                );
                var items = (Result<IReadOnlyList<(string Id, string OrderId)>, SqlError>.Ok<
                    IReadOnlyList<(string Id, string OrderId)>,
                    SqlError
                >)queryResult;
                var item = items.Value[0];

                var result = await tx.UpdateOrderItemAsync(
                        item.Id,
                        item.OrderId,
                        "Updated Widget",
                        10.0,
                        50.00,
                        500.00
                    )
                    .ConfigureAwait(false);
                Assert.Equal(1, Assert.IsType<AffectedRowsOk>(result).Value);
            })
            .ConfigureAwait(false);
    }

    /// <summary>Checks affected rows for UpdateCustomerAsync_WithNonExistentId_ReturnsZeroRows.</summary>
    [Fact]
    public async Task UpdateCustomerAsync_WithNonExistentId_ReturnsZeroRows()
    {
        await SetupSchemaAndSeed().ConfigureAwait(false);

        await _connection
            .Transact(async tx =>
            {
                var result = await tx.UpdateCustomerAsync(
                        "nonexistent-id",
                        "Updated",
                        "u@t.com",
                        "555-0000",
                        "2024-01-01"
                    )
                    .ConfigureAwait(false);
                Assert.Equal(0, Assert.IsType<AffectedRowsOk>(result).Value);
            })
            .ConfigureAwait(false);
    }

    /// <summary>Checks affected rows for UpdateInvoiceAsync_WithNonExistentId_ReturnsZeroRows.</summary>
    [Fact]
    public async Task UpdateInvoiceAsync_WithNonExistentId_ReturnsZeroRows()
    {
        await SetupSchemaAndSeed().ConfigureAwait(false);

        await _connection
            .Transact(async tx =>
            {
                var result = await tx.UpdateInvoiceAsync(
                        "nonexistent",
                        "INV-X",
                        "2024-01-01",
                        "X",
                        "x@t.com",
                        0.0,
                        0.0,
                        "n"
                    )
                    .ConfigureAwait(false);
                Assert.Equal(0, Assert.IsType<AffectedRowsOk>(result).Value);
            })
            .ConfigureAwait(false);
    }

    /// <summary>Checks affected rows for UpdateAddressAsync_WithNonExistentId_ReturnsZeroRows.</summary>
    [Fact]
    public async Task UpdateAddressAsync_WithNonExistentId_ReturnsZeroRows()
    {
        await SetupSchemaAndSeed().ConfigureAwait(false);

        await _connection
            .Transact(async tx =>
            {
                var result = await tx.UpdateAddressAsync(
                        "nonexistent",
                        "cust-1",
                        "St",
                        "City",
                        "ST",
                        "00000",
                        "US"
                    )
                    .ConfigureAwait(false);
                Assert.Equal(0, Assert.IsType<AffectedRowsOk>(result).Value);
            })
            .ConfigureAwait(false);
    }

    /// <summary>Checks affected rows for UpdateInvoiceLineAsync_WithNonExistentId_ReturnsZeroRows.</summary>
    [Fact]
    public async Task UpdateInvoiceLineAsync_WithNonExistentId_ReturnsZeroRows()
    {
        await SetupSchemaAndSeed().ConfigureAwait(false);

        await _connection
            .Transact(async tx =>
            {
                var result = await tx.UpdateInvoiceLineAsync(
                        "nonexistent",
                        "inv-1",
                        "Desc",
                        1.0,
                        10.0,
                        10.0,
                        0.0,
                        "n"
                    )
                    .ConfigureAwait(false);
                Assert.Equal(0, Assert.IsType<AffectedRowsOk>(result).Value);
            })
            .ConfigureAwait(false);
    }

    /// <summary>Checks affected rows for UpdateOrderItemAsync_WithNonExistentId_ReturnsZeroRows.</summary>
    [Fact]
    public async Task UpdateOrderItemAsync_WithNonExistentId_ReturnsZeroRows()
    {
        await SetupSchemaAndSeed().ConfigureAwait(false);

        await _connection
            .Transact(async tx =>
            {
                var result = await tx.UpdateOrderItemAsync(
                        "nonexistent",
                        "ord-1",
                        "Product",
                        1.0,
                        10.0,
                        10.0
                    )
                    .ConfigureAwait(false);
                Assert.Equal(0, Assert.IsType<AffectedRowsOk>(result).Value);
            })
            .ConfigureAwait(false);
    }
}
