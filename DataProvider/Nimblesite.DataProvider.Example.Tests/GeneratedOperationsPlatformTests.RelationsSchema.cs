using Nimblesite.DataProvider.Migration.Core;

namespace Nimblesite.DataProvider.Example.Tests;

public sealed partial class GeneratedOperationsPlatformTests
{
    private static SchemaDefinition RelationsSchema(string schema) =>
        new()
        {
            Name = "generated_relations",
            Tables =
            [
                CustomerTable(schema),
                EntityTable(
                    schema,
                    "Invoice",
                    [
                        UuidColumn("Id"),
                        TextColumn("InvoiceNumber"),
                        TextColumn("InvoiceDate"),
                        TextColumn("CustomerName"),
                        NullableTextColumn("CustomerEmail"),
                        DecimalColumn("TotalAmount"),
                        DecimalColumn("DiscountAmount", nullable: true),
                        NullableTextColumn("Notes"),
                    ]
                ),
                EntityTable(
                    schema,
                    "InvoiceLine",
                    [
                        UuidColumn("Id"),
                        UuidColumn("InvoiceId"),
                        TextColumn("Description"),
                        DecimalColumn("Quantity"),
                        DecimalColumn("UnitPrice"),
                        DecimalColumn("Amount"),
                        DecimalColumn("DiscountPercentage", nullable: true),
                        NullableTextColumn("Notes"),
                    ],
                    [RelationForeignKey(schema, "InvoiceId", "Invoice")]
                ),
                EntityTable(
                    schema,
                    "Address",
                    [
                        UuidColumn("Id"),
                        UuidColumn("CustomerId"),
                        TextColumn("Street"),
                        TextColumn("City"),
                        TextColumn("State"),
                        TextColumn("ZipCode"),
                        TextColumn("Country"),
                    ],
                    [RelationForeignKey(schema, "CustomerId", "Customer")]
                ),
                EntityTable(
                    schema,
                    "Orders",
                    [
                        UuidColumn("Id"),
                        TextColumn("OrderNumber"),
                        TextColumn("OrderDate"),
                        UuidColumn("CustomerId"),
                        DecimalColumn("TotalAmount"),
                        TextColumn("Status"),
                    ],
                    [RelationForeignKey(schema, "CustomerId", "Customer")]
                ),
                EntityTable(
                    schema,
                    "OrderItem",
                    [
                        UuidColumn("Id"),
                        UuidColumn("OrderId"),
                        TextColumn("ProductName"),
                        DecimalColumn("Quantity"),
                        DecimalColumn("Price"),
                        DecimalColumn("Subtotal"),
                    ],
                    [RelationForeignKey(schema, "OrderId", "Orders")]
                ),
            ],
        };

    private static TableDefinition EntityTable(
        string schema,
        string name,
        ColumnDefinition[] columns,
        ForeignKeyDefinition[]? foreignKeys = null
    ) =>
        new()
        {
            Schema = schema,
            Name = name,
            Columns = columns,
            PrimaryKey = new PrimaryKeyDefinition { Columns = ["Id"] },
            ForeignKeys = foreignKeys ?? [],
        };

    private static ForeignKeyDefinition RelationForeignKey(
        string schema,
        string column,
        string table
    ) =>
        new()
        {
            Columns = [column],
            ReferencedSchema = schema,
            ReferencedTable = table,
            ReferencedColumns = ["Id"],
        };

    private static ColumnDefinition NullableTextColumn(string name) =>
        new()
        {
            Name = name,
            Type = PortableTypes.Text,
            IsNullable = true,
        };

    private static ColumnDefinition DecimalColumn(string name, bool nullable = false) =>
        new()
        {
            Name = name,
            Type = PortableTypes.Decimal(18, 2),
            IsNullable = nullable,
        };
}
