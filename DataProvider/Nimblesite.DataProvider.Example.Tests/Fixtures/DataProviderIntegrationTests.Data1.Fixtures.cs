// Implements [CI-DESLOP-INDEPENDENT-DATA].
namespace Nimblesite.DataProvider.Example.Tests;

public partial class DataProviderIntegrationTests
{
    private const string Data_SetupTestDatabase_1 = """
        INSERT INTO Invoice (Id, InvoiceNumber, InvoiceDate, CustomerName, CustomerEmail, TotalAmount, DiscountAmount, Notes) VALUES
        ('inv-1', 'INV-001', '2024-01-15', 'Acme Corp', 'accounting@acme.com', 1250.00, NULL, 'Test invoice'),
        ('inv-2', 'INV-002', '2024-01-16', 'Acme Corp', 'accounting@acme.com', 850.75, 25.00, NULL),
        ('inv-3', 'INV-003', '2024-01-17', 'Acme Corp', 'accounting@acme.com', 2100.25, 100.00, 'Large order discount');

        INSERT INTO InvoiceLine (Id, InvoiceId, Description, Quantity, UnitPrice, Amount, DiscountPercentage, Notes) VALUES
        ('line-1', 'inv-1', 'Software License', 1.0, 1000.00, 1000.00, NULL, NULL),
        ('line-2', 'inv-1', 'Support Package', 1.0, 250.00, 250.00, 10.0, 'First year support'),
        ('line-3', 'inv-2', 'Consulting Hours', 5.0, 150.00, 750.00, NULL, NULL),
        ('line-4', 'inv-2', 'Travel Expenses', 1.0, 100.75, 100.75, NULL, 'Reimbursement'),
        ('line-5', 'inv-3', 'Hardware Components', 10.0, 125.50, 1255.00, 5.0, 'Bulk discount'),
        ('line-6', 'inv-3', 'Installation Service', 3.0, 281.75, 845.25, NULL, NULL);

        INSERT INTO Customer (Id, CustomerName, Email, Phone, CreatedDate) VALUES
        ('cust-1', 'Acme Corp', 'contact@acme.com', '555-0100', '2024-01-01'),
        ('cust-2', 'Tech Solutions', 'info@techsolutions.com', '555-0200', '2024-01-02');

        INSERT INTO Address (Id, CustomerId, Street, City, State, ZipCode, Country) VALUES
        ('addr-1', 'cust-1', '123 Business Ave', 'New York', 'NY', '10001', 'USA'),
        ('addr-2', 'cust-1', '456 Main St', 'Albany', 'NY', '12201', 'USA'),
        ('addr-3', 'cust-2', '789 Tech Blvd', 'San Francisco', 'CA', '94105', 'USA');

        INSERT INTO Orders (Id, OrderNumber, OrderDate, CustomerId, TotalAmount, Status) VALUES
        ('ord-1', 'ORD-001', '2024-01-10', 'cust-1', 500.00, 'Completed'),
        ('ord-2', 'ORD-002', '2024-01-11', 'cust-2', 750.00, 'Processing');

        INSERT INTO OrderItem (Id, OrderId, ProductName, Quantity, Price, Subtotal) VALUES
        ('item-1', 'ord-1', 'Widget A', 2.0, 100.00, 200.00),
        ('item-2', 'ord-1', 'Widget B', 3.0, 100.00, 300.00),
        ('item-3', 'ord-2', 'Service Package', 1.0, 750.00, 750.00);
        """;

    private const string Data_SetupEmptyDatabase_1 = """
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
}
