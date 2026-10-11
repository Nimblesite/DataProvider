// Implements [CI-DESLOP-INDEPENDENT-DATA].
namespace Nimblesite.DataProvider.Tests;

public partial class QueryBuilderE2ETests
{
    private const string Data_CreateSchemaAndSeed_1 = """
        CREATE TABLE Products (
            Id TEXT PRIMARY KEY,
            Name TEXT NOT NULL,
            Price REAL NOT NULL,
            Quantity INTEGER NOT NULL,
            Category TEXT NOT NULL
        );
        CREATE TABLE Categories (
            Name TEXT PRIMARY KEY,
            Description TEXT NOT NULL
        );
        CREATE TABLE Suppliers (
            Id TEXT PRIMARY KEY,
            CompanyName TEXT NOT NULL,
            Country TEXT NOT NULL
        );
        CREATE TABLE ProductSuppliers (
            ProductId TEXT NOT NULL,
            SupplierId TEXT NOT NULL,
            PRIMARY KEY (ProductId, SupplierId)
        );
        """;

    private const string Data_CreateSchemaAndSeed_2 = """
        INSERT INTO Products VALUES ('p1', 'Widget A', 10.00, 100, 'Electronics');
        INSERT INTO Products VALUES ('p2', 'Widget B', 25.50, 50, 'Electronics');
        INSERT INTO Products VALUES ('p3', 'Gadget X', 99.99, 10, 'Gadgets');
        INSERT INTO Products VALUES ('p4', 'Gadget Y', 149.99, 5, 'Gadgets');
        INSERT INTO Products VALUES ('p5', 'Tool Alpha', 35.00, 75, 'Tools');
        INSERT INTO Products VALUES ('p6', 'Tool Beta', 45.00, 30, 'Tools');
        INSERT INTO Products VALUES ('p7', 'Tool Gamma', 15.00, 200, 'Tools');
        INSERT INTO Products VALUES ('p8', 'Premium Widget', 500.00, 2, 'Electronics');
        """;

    private const string Data_CreateSchemaAndSeed_3 = """
        INSERT INTO Categories VALUES ('Electronics', 'Electronic devices and components');
        INSERT INTO Categories VALUES ('Gadgets', 'Innovative gadgets');
        INSERT INTO Categories VALUES ('Tools', 'Professional tools');
        """;

    private const string Data_CreateSchemaAndSeed_4 = """
        INSERT INTO Suppliers VALUES ('s1', 'Acme Corp', 'US');
        INSERT INTO Suppliers VALUES ('s2', 'Global Parts', 'UK');
        INSERT INTO ProductSuppliers VALUES ('p1', 's1');
        INSERT INTO ProductSuppliers VALUES ('p1', 's2');
        INSERT INTO ProductSuppliers VALUES ('p3', 's1');
        """;
}
