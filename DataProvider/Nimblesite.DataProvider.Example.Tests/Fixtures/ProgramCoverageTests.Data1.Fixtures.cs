// Implements [CI-DESLOP-INDEPENDENT-DATA].
namespace Nimblesite.DataProvider.Example.Tests;

public partial class ProgramCoverageTests
{
    private const string Data_SetupTestDatabase_1 = """
            INSERT INTO Invoice (Id, InvoiceNumber, InvoiceDate, CustomerName, CustomerEmail, TotalAmount, DiscountAmount, Notes) VALUES
            ('inv-1', 'INV-001', '2024-01-15', 'Acme Corp', 'accounting@acme.com', 1250.00, NULL, 'Test invoice');
            INSERT INTO InvoiceLine (Id, InvoiceId, Description, Quantity, UnitPrice, Amount, DiscountPercentage, Notes) VALUES
            ('line-1', 'inv-1', 'Software License', 1.0, 1000.00, 1000.00, NULL, NULL),
            ('line-2', 'inv-1', 'Support Package', 1.0, 250.00, 250.00, 10.0, 'First year support');
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

    private const string Data_SetupTestDatabaseWithNullableFields_1 = """
            CREATE TABLE IF NOT EXISTS Customer (
                Id TEXT PRIMARY KEY,
                CustomerName TEXT NOT NULL,
                Email TEXT NULL,
                Phone TEXT NULL,
                CreatedDate TEXT NOT NULL
            );
            """;

    private const string Data_SetupTestDatabaseWithNullableFields_2 = """
            INSERT INTO Customer (Id, CustomerName, Email, Phone, CreatedDate) VALUES
            ('cust-1', 'Full Contact Corp', 'full@test.com', '555-1111', '2024-01-01'),
            ('cust-2', 'No Email Corp', NULL, '555-9999', '2024-01-02'),
            ('cust-3', 'No Phone Corp', 'nophone@test.com', NULL, '2024-01-03'),
            ('cust-4', 'No Contact Corp', NULL, NULL, '2024-01-04');
            """;
}
