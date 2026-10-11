// Implements [CI-DESLOP-INDEPENDENT-DATA].
namespace Nimblesite.DataProvider.Tests;

public partial class SelectStatementLinqE2ETests
{
    private const string Data_CreateSchemaAndSeed_1 = """
        CREATE TABLE Products (
            Id TEXT PRIMARY KEY,
            Name TEXT NOT NULL,
            Price REAL NOT NULL,
            Quantity INTEGER NOT NULL,
            Category TEXT NOT NULL
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
}
