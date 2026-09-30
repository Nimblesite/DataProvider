using System.Data.Common;
using Xunit;
using AffectedRowsOk = Outcome.Result<int, Nimblesite.Sql.Model.SqlError>.Ok<
    int,
    Nimblesite.Sql.Model.SqlError
>;

namespace Nimblesite.DataProvider.Example.Tests;

/// <summary>Shared generated operations coverage for related records.</summary>
public sealed partial class GeneratedOperationsPlatformTests
{
    /// <summary>Inserts, updates, and reads every generated related entity on all providers.</summary>
    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgres")]
    [InlineData("sqlserver")]
    public async Task GeneratedRelatedOperations_PersistAndUpdateEveryEntity(string provider) =>
        await WithProviderAsync(provider, VerifyRelationsAsync).ConfigureAwait(false);

    private static async Task VerifyRelationsAsync(
        DbConnection connection,
        string provider,
        string output,
        string schema
    )
    {
        MigrateSchema(provider, output, RelationsSchema(provider, schema));
        var ids = new RelationIds(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid()
        );
        using (var transaction = await connection.BeginTransactionAsync().ConfigureAwait(false))
        {
            await InsertRelationsAsync(transaction, ids).ConfigureAwait(false);
            await transaction.CommitAsync().ConfigureAwait(false);
        }
        AssertInsertedRelations(connection, ids);

        using (var transaction = await connection.BeginTransactionAsync().ConfigureAwait(false))
        {
            await UpdateRelationsAsync(transaction, ids).ConfigureAwait(false);
            await transaction.CommitAsync().ConfigureAwait(false);
        }
        AssertUpdatedRelations(connection, ids);
    }

    private static async Task InsertRelationsAsync(DbTransaction tx, RelationIds ids)
    {
        var customer = await tx.InsertCustomerAsync(
                ids.Customer.ToString(),
                "Acme Customer",
                "acme@example.test",
                "555-0100",
                "2024-01-01"
            )
            .ConfigureAwait(false);
        Assert.Equal(1, Assert.IsType<AffectedRowsOk>(customer).Value);
        var invoice = await tx.InsertInvoiceAsync(
                ids.Invoice.ToString(),
                "INV-001",
                "2024-01-15",
                "Acme Customer",
                "billing@example.test",
                120.00,
                null,
                "Initial invoice"
            )
            .ConfigureAwait(false);
        Assert.Equal(1, Assert.IsType<AffectedRowsOk>(invoice).Value);
        var line = await tx.InsertInvoiceLineAsync(
                ids.InvoiceLine.ToString(),
                ids.Invoice.ToString(),
                "Initial line",
                2.0,
                60.00,
                120.00,
                0.0,
                "Initial line note"
            )
            .ConfigureAwait(false);
        Assert.Equal(1, Assert.IsType<AffectedRowsOk>(line).Value);
        var address = await tx.InsertAddressAsync(
                ids.Address.ToString(),
                ids.Customer.ToString(),
                "100 First St",
                "Sydney",
                "NSW",
                "2000",
                "Australia"
            )
            .ConfigureAwait(false);
        Assert.Equal(1, Assert.IsType<AffectedRowsOk>(address).Value);
        var order = await tx.InsertOrdersAsync(
                ids.Order.ToString(),
                "ORD-001",
                "2024-01-20",
                ids.Customer.ToString(),
                90.00,
                "Pending"
            )
            .ConfigureAwait(false);
        Assert.Equal(1, Assert.IsType<AffectedRowsOk>(order).Value);
        var item = await tx.InsertOrderItemAsync(
                ids.OrderItem.ToString(),
                ids.Order.ToString(),
                "Widget",
                3.0,
                30.00,
                90.00
            )
            .ConfigureAwait(false);
        Assert.Equal(1, Assert.IsType<AffectedRowsOk>(item).Value);
    }

    private static async Task UpdateRelationsAsync(DbTransaction tx, RelationIds ids)
    {
        var invoice = await tx.UpdateInvoiceAsync(
                ids.Invoice.ToString(),
                "INV-UPDATED",
                "2025-02-15",
                "Acme Updated",
                "updated-billing@example.test",
                200.00,
                25.00,
                "Updated invoice"
            )
            .ConfigureAwait(false);
        Assert.Equal(1, Assert.IsType<AffectedRowsOk>(invoice).Value);
        var line = await tx.UpdateInvoiceLineAsync(
                ids.InvoiceLine.ToString(),
                ids.Invoice.ToString(),
                "Updated line",
                4.0,
                50.00,
                200.00,
                5.0,
                "Updated line note"
            )
            .ConfigureAwait(false);
        Assert.Equal(1, Assert.IsType<AffectedRowsOk>(line).Value);
        var address = await tx.UpdateAddressAsync(
                ids.Address.ToString(),
                ids.Customer.ToString(),
                "200 Second Ave",
                "Melbourne",
                "VIC",
                "3000",
                "Australia"
            )
            .ConfigureAwait(false);
        Assert.Equal(1, Assert.IsType<AffectedRowsOk>(address).Value);
        var order = await tx.UpdateOrdersAsync(
                ids.Order.ToString(),
                "ORD-UPDATED",
                "2025-02-20",
                ids.Customer.ToString(),
                150.00,
                "Completed"
            )
            .ConfigureAwait(false);
        Assert.Equal(1, Assert.IsType<AffectedRowsOk>(order).Value);
        var item = await tx.UpdateOrderItemAsync(
                ids.OrderItem.ToString(),
                ids.Order.ToString(),
                "Updated widget",
                5.0,
                30.00,
                150.00
            )
            .ConfigureAwait(false);
        Assert.Equal(1, Assert.IsType<AffectedRowsOk>(item).Value);

        var missing = Guid.NewGuid().ToString();
        var missingInvoice = await tx.UpdateInvoiceAsync(
                missing,
                "INV-MISSING",
                "2025-02-15",
                "Missing",
                "missing@example.test",
                1.00,
                null,
                "Missing"
            )
            .ConfigureAwait(false);
        Assert.Equal(0, Assert.IsType<AffectedRowsOk>(missingInvoice).Value);
        var missingLine = await tx.UpdateInvoiceLineAsync(
                missing,
                ids.Invoice.ToString(),
                "Missing",
                1.0,
                1.0,
                1.0,
                0.0,
                "Missing"
            )
            .ConfigureAwait(false);
        Assert.Equal(0, Assert.IsType<AffectedRowsOk>(missingLine).Value);
        var missingAddress = await tx.UpdateAddressAsync(
                missing,
                ids.Customer.ToString(),
                "Missing",
                "Missing",
                "NSW",
                "0000",
                "Australia"
            )
            .ConfigureAwait(false);
        Assert.Equal(0, Assert.IsType<AffectedRowsOk>(missingAddress).Value);
        var missingOrder = await tx.UpdateOrdersAsync(
                missing,
                "ORD-MISSING",
                "2025-02-20",
                ids.Customer.ToString(),
                1.00,
                "Missing"
            )
            .ConfigureAwait(false);
        Assert.Equal(0, Assert.IsType<AffectedRowsOk>(missingOrder).Value);
        var missingItem = await tx.UpdateOrderItemAsync(
                missing,
                ids.Order.ToString(),
                "Missing",
                1.0,
                1.0,
                1.0
            )
            .ConfigureAwait(false);
        Assert.Equal(0, Assert.IsType<AffectedRowsOk>(missingItem).Value);
    }

    private sealed record RelationIds(
        Guid Customer,
        Guid Invoice,
        Guid InvoiceLine,
        Guid Address,
        Guid Order,
        Guid OrderItem
    );
}
