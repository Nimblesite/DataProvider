using System.Data.Common;
using System.Globalization;

namespace Nimblesite.DataProvider.Example.Tests;

public sealed partial class GeneratedOperationsPlatformTests
{
    private static void AssertInsertedRelations(DbConnection connection, RelationIds ids) =>
        AssertRelationRows(connection, RelationRowsInserted(ids: ids));

    private static void AssertUpdatedRelations(DbConnection connection, RelationIds ids) =>
        AssertRelationRows(connection, RelationRowsUpdated(ids: ids));

    private static void AssertRelationRows(
        DbConnection connection,
        (string Table, (string Column, object? Value)[] Values)[] rows
    )
    {
        foreach (var (table, values) in rows)
        {
            AssertRow(connection, table, values);
        }
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
