using System.Data.Common;
using System.Globalization;
using Xunit;

namespace Nimblesite.DataProvider.Example.Tests;

public sealed partial class GeneratedOperationsPlatformTests
{
    private static void AssertInsertedRelations(DbConnection connection, RelationIds ids)
    {
        AssertRow(
            connection,
            "Customer",
            ("Id", ids.Customer),
            ("CustomerName", "Acme Customer"),
            ("Email", "acme@example.test")
        );
        AssertRow(
            connection,
            "Invoice",
            ("Id", ids.Invoice),
            ("InvoiceNumber", "INV-001"),
            ("TotalAmount", 120m),
            ("DiscountAmount", null)
        );
        AssertRow(
            connection,
            "InvoiceLine",
            ("Id", ids.InvoiceLine),
            ("InvoiceId", ids.Invoice),
            ("Description", "Initial line"),
            ("Quantity", 2m)
        );
        AssertRow(
            connection,
            "Address",
            ("Id", ids.Address),
            ("CustomerId", ids.Customer),
            ("Street", "100 First St"),
            ("City", "Sydney")
        );
        AssertRow(
            connection,
            "Orders",
            ("Id", ids.Order),
            ("CustomerId", ids.Customer),
            ("OrderNumber", "ORD-001"),
            ("Status", "Pending")
        );
        AssertRow(
            connection,
            "OrderItem",
            ("Id", ids.OrderItem),
            ("OrderId", ids.Order),
            ("ProductName", "Widget"),
            ("Subtotal", 90m)
        );
    }

    private static void AssertUpdatedRelations(DbConnection connection, RelationIds ids)
    {
        AssertRow(
            connection,
            "Customer",
            ("Id", ids.Customer),
            ("CustomerName", "Acme Customer"),
            ("Email", "acme@example.test")
        );
        AssertRow(
            connection,
            "Invoice",
            ("Id", ids.Invoice),
            ("InvoiceNumber", "INV-UPDATED"),
            ("CustomerName", "Acme Updated"),
            ("TotalAmount", 200m),
            ("DiscountAmount", 25m),
            ("Notes", "Updated invoice")
        );
        AssertRow(
            connection,
            "InvoiceLine",
            ("Id", ids.InvoiceLine),
            ("InvoiceId", ids.Invoice),
            ("Description", "Updated line"),
            ("Quantity", 4m),
            ("Amount", 200m)
        );
        AssertRow(
            connection,
            "Address",
            ("Id", ids.Address),
            ("CustomerId", ids.Customer),
            ("Street", "200 Second Ave"),
            ("City", "Melbourne"),
            ("ZipCode", "3000")
        );
        AssertRow(
            connection,
            "Orders",
            ("Id", ids.Order),
            ("CustomerId", ids.Customer),
            ("OrderNumber", "ORD-UPDATED"),
            ("TotalAmount", 150m),
            ("Status", "Completed")
        );
        AssertRow(
            connection,
            "OrderItem",
            ("Id", ids.OrderItem),
            ("OrderId", ids.Order),
            ("ProductName", "Updated widget"),
            ("Quantity", 5m),
            ("Subtotal", 150m)
        );
    }

    private static void AssertRow(
        DbConnection connection,
        string table,
        params (string Column, object? Expected)[] expected
    )
    {
        using var command = connection.CreateCommand();
        switch (table)
        {
            case "Customer":
                command.CommandText = """SELECT * FROM "Customer" """;
                break;
            case "Invoice":
                command.CommandText = """SELECT * FROM "Invoice" """;
                break;
            case "InvoiceLine":
                command.CommandText = """SELECT * FROM "InvoiceLine" """;
                break;
            case "Address":
                command.CommandText = """SELECT * FROM "Address" """;
                break;
            case "Orders":
                command.CommandText = """SELECT * FROM "Orders" """;
                break;
            case "OrderItem":
                command.CommandText = """SELECT * FROM "OrderItem" """;
                break;
            default:
                Assert.Fail($"Unsupported generated table: {table}");
                break;
        }
        using var reader = command.ExecuteReader();
        Assert.True(reader.Read(), $"Missing {table} row");
        foreach (var (column, value) in expected)
        {
            AssertValue(reader, table, column, value);
        }
        Assert.False(reader.Read(), $"Unexpected second {table} row");
    }

    private static void AssertValue(
        DbDataReader reader,
        string table,
        string column,
        object? expected
    )
    {
        var index = reader.GetOrdinal(column);
        if (expected is null)
        {
            Assert.True(reader.IsDBNull(index), $"Expected {table}.{column} to be NULL");
            return;
        }
        Assert.False(reader.IsDBNull(index), $"Unexpected NULL in {table}.{column}");
        var actual = reader.GetValue(index);
        switch (expected)
        {
            case Guid guid:
                Assert.Equal(
                    guid,
                    Guid.Parse(Convert.ToString(actual, CultureInfo.InvariantCulture) ?? "")
                );
                break;
            case decimal number:
                Assert.Equal(number, Convert.ToDecimal(actual, CultureInfo.InvariantCulture));
                break;
            case string value:
                Assert.Equal(value, Convert.ToString(actual, CultureInfo.InvariantCulture));
                break;
            default:
                Assert.Fail($"Unsupported expected value for {table}.{column}");
                break;
        }
    }
}
