// Implements [CI-DESLOP-INDEPENDENT-DATA].
// Pure expected-row fixtures; the relation assertion algorithm remains analysed.
namespace Nimblesite.DataProvider.Example.Tests;

public sealed partial class GeneratedOperationsPlatformTests
{
    private static (string Table, (string Column, object? Value)[] Values)[] RelationRowsInserted(
        RelationIds ids
    ) =>
        [
            (
                "Customer",
                [
                    ("Id", ids.Customer),
                    ("CustomerName", "Acme Customer"),
                    ("Email", "acme@example.test"),
                ]
            ),
            (
                "Invoice",
                [
                    ("Id", ids.Invoice),
                    ("InvoiceNumber", "INV-001"),
                    ("TotalAmount", 120m),
                    ("DiscountAmount", null),
                ]
            ),
            (
                "InvoiceLine",
                [
                    ("Id", ids.InvoiceLine),
                    ("InvoiceId", ids.Invoice),
                    ("Description", "Initial line"),
                    ("Quantity", 2m),
                ]
            ),
            (
                "Address",
                [
                    ("Id", ids.Address),
                    ("CustomerId", ids.Customer),
                    ("Street", "100 First St"),
                    ("City", "Sydney"),
                ]
            ),
            (
                "Orders",
                [
                    ("Id", ids.Order),
                    ("CustomerId", ids.Customer),
                    ("OrderNumber", "ORD-001"),
                    ("Status", "Pending"),
                ]
            ),
            (
                "OrderItem",
                [
                    ("Id", ids.OrderItem),
                    ("OrderId", ids.Order),
                    ("ProductName", "Widget"),
                    ("Subtotal", 90m),
                ]
            ),
        ];

    private static (string Table, (string Column, object? Value)[] Values)[] RelationRowsUpdated(
        RelationIds ids
    ) =>
        [
            (
                "Customer",
                [
                    ("Id", ids.Customer),
                    ("CustomerName", "Acme Customer"),
                    ("Email", "acme@example.test"),
                ]
            ),
            (
                "Invoice",
                [
                    ("Id", ids.Invoice),
                    ("InvoiceNumber", "INV-UPDATED"),
                    ("CustomerName", "Acme Updated"),
                    ("TotalAmount", 200m),
                    ("DiscountAmount", 25m),
                    ("Notes", "Updated invoice"),
                ]
            ),
            (
                "InvoiceLine",
                [
                    ("Id", ids.InvoiceLine),
                    ("InvoiceId", ids.Invoice),
                    ("Description", "Updated line"),
                    ("Quantity", 4m),
                    ("Amount", 200m),
                ]
            ),
            (
                "Address",
                [
                    ("Id", ids.Address),
                    ("CustomerId", ids.Customer),
                    ("Street", "200 Second Ave"),
                    ("City", "Melbourne"),
                    ("ZipCode", "3000"),
                ]
            ),
            (
                "Orders",
                [
                    ("Id", ids.Order),
                    ("CustomerId", ids.Customer),
                    ("OrderNumber", "ORD-UPDATED"),
                    ("TotalAmount", 150m),
                    ("Status", "Completed"),
                ]
            ),
            (
                "OrderItem",
                [
                    ("Id", ids.OrderItem),
                    ("OrderId", ids.Order),
                    ("ProductName", "Updated widget"),
                    ("Quantity", 5m),
                    ("Subtotal", 150m),
                ]
            ),
        ];
}
