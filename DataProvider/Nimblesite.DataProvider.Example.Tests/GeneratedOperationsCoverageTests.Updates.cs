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
                var customerId = VerifySharedContinuation2(
                    input0: tx,
                    value0: "SELECT Id FROM Customer LIMIT 1"
                );

                var result = await tx.UpdateCustomerAsync(
                        customerId,
                        "Updated Customer",
                        "updated@test.com",
                        "555-9999",
                        "2024-06-01"
                    )
                    .ConfigureAwait(false);
                Assert.Equal(
                    1,
                    global::Tests.Shared.ResultAssertions.ExactOk(actual: result).Value
                );
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
                var invoiceId = VerifySharedContinuation2(
                    input0: tx,
                    value0: "SELECT Id FROM Invoice LIMIT 1"
                );

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
                Assert.Equal(
                    1,
                    global::Tests.Shared.ResultAssertions.ExactOk(actual: result).Value
                );
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
                var line = CoreCoverageTests.SelectIdAndReference(
                    transaction: tx,
                    sql: "SELECT Id, InvoiceId FROM InvoiceLine LIMIT 1"
                );

                var result = await tx.UpdateInvoiceLineAsync(
                        line.Id,
                        line.Reference,
                        "Updated Description",
                        3.0,
                        100.00,
                        300.00,
                        10.0,
                        "Updated notes"
                    )
                    .ConfigureAwait(false);
                Assert.Equal(
                    1,
                    global::Tests.Shared.ResultAssertions.ExactOk(actual: result).Value
                );
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
                var addr = CoreCoverageTests.SelectIdAndReference(
                    transaction: tx,
                    sql: "SELECT Id, CustomerId FROM Address LIMIT 1"
                );

                var result = await tx.UpdateAddressAsync(
                        addr.Id,
                        addr.Reference,
                        "200 Updated Ave",
                        "UpdatedCity",
                        "UC",
                        "67890",
                        "USA"
                    )
                    .ConfigureAwait(false);
                Assert.Equal(
                    1,
                    global::Tests.Shared.ResultAssertions.ExactOk(actual: result).Value
                );
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
                var order = CoreCoverageTests.SelectIdAndReference(
                    transaction: tx,
                    sql: "SELECT Id, CustomerId FROM Orders LIMIT 1"
                );

                var result = await tx.UpdateOrdersAsync(
                        order.Id,
                        "ORD-UPDATED",
                        "2024-07-01",
                        order.Reference,
                        1500.00,
                        "Completed"
                    )
                    .ConfigureAwait(false);
                Assert.Equal(
                    1,
                    global::Tests.Shared.ResultAssertions.ExactOk(actual: result).Value
                );
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
                var item = CoreCoverageTests.SelectIdAndReference(
                    transaction: tx,
                    sql: "SELECT Id, OrderId FROM OrderItem LIMIT 1"
                );

                var result = await tx.UpdateOrderItemAsync(
                        item.Id,
                        item.Reference,
                        "Updated Widget",
                        10.0,
                        50.00,
                        500.00
                    )
                    .ConfigureAwait(false);
                Assert.Equal(
                    1,
                    global::Tests.Shared.ResultAssertions.ExactOk(actual: result).Value
                );
            })
            .ConfigureAwait(false);
    }

    /// <summary>Checks affected rows for UpdateCustomerAsync_WithNonExistentId_ReturnsZeroRows.</summary>
    [Fact]
    public async Task UpdateCustomerAsync_WithNonExistentId_ReturnsZeroRows()
    {
        await AssertAffectedRows(
                execute: tx =>
                    tx.UpdateCustomerAsync(
                        "nonexistent-id",
                        "Updated",
                        "u@t.com",
                        "555-0000",
                        "2024-01-01"
                    ),
                expectedRows: 0,
                seed: true
            )
            .ConfigureAwait(false);
    }

    /// <summary>Checks affected rows for UpdateInvoiceAsync_WithNonExistentId_ReturnsZeroRows.</summary>
    [Fact]
    public async Task UpdateInvoiceAsync_WithNonExistentId_ReturnsZeroRows()
    {
        await AssertAffectedRows(
                execute: tx =>
                    tx.UpdateInvoiceAsync(
                        "nonexistent",
                        "INV-X",
                        "2024-01-01",
                        "X",
                        "x@t.com",
                        0.0,
                        0.0,
                        "n"
                    ),
                expectedRows: 0,
                seed: true
            )
            .ConfigureAwait(false);
    }

    /// <summary>Checks affected rows for UpdateAddressAsync_WithNonExistentId_ReturnsZeroRows.</summary>
    [Fact]
    public async Task UpdateAddressAsync_WithNonExistentId_ReturnsZeroRows()
    {
        await AssertAffectedRows(
                execute: tx =>
                    tx.UpdateAddressAsync(
                        "nonexistent",
                        "cust-1",
                        "St",
                        "City",
                        "ST",
                        "00000",
                        "US"
                    ),
                expectedRows: 0,
                seed: true
            )
            .ConfigureAwait(false);
    }

    /// <summary>Checks affected rows for UpdateInvoiceLineAsync_WithNonExistentId_ReturnsZeroRows.</summary>
    [Fact]
    public async Task UpdateInvoiceLineAsync_WithNonExistentId_ReturnsZeroRows()
    {
        await AssertAffectedRows(
                execute: tx =>
                    tx.UpdateInvoiceLineAsync(
                        "nonexistent",
                        "inv-1",
                        "Desc",
                        1.0,
                        10.0,
                        10.0,
                        0.0,
                        "n"
                    ),
                expectedRows: 0,
                seed: true
            )
            .ConfigureAwait(false);
    }

    /// <summary>Checks affected rows for UpdateOrderItemAsync_WithNonExistentId_ReturnsZeroRows.</summary>
    [Fact]
    public async Task UpdateOrderItemAsync_WithNonExistentId_ReturnsZeroRows()
    {
        await AssertAffectedRows(
                execute: tx =>
                    tx.UpdateOrderItemAsync("nonexistent", "ord-1", "Product", 1.0, 10.0, 10.0),
                expectedRows: 0,
                seed: true
            )
            .ConfigureAwait(false);
    }
}
