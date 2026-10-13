// Implements [CI-DESLOP-INDEPENDENT-DATA].
namespace Nimblesite.DataProvider.Example.Tests;

public partial class GeneratedOperationsCoverageTests
{
    private const string Data_SetupSchema_1 = """
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

    private const string Data_SetupSchemaAndSeed_1 = """
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
}
