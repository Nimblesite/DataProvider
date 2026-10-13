// Implements [CI-DESLOP-INDEPENDENT-DATA].
namespace Nimblesite.DataProvider.Tests;

public partial class SqliteTransactionE2ETests
{
    private const string Data_CreateSchema_1 = """
        CREATE TABLE Orders (
            Id TEXT PRIMARY KEY,
            CustomerId TEXT NOT NULL,
            Total REAL NOT NULL,
            Status TEXT NOT NULL DEFAULT 'pending'
        );
        CREATE TABLE OrderItems (
            Id TEXT PRIMARY KEY,
            OrderId TEXT NOT NULL,
            ProductName TEXT NOT NULL,
            Quantity INTEGER NOT NULL,
            UnitPrice REAL NOT NULL,
            FOREIGN KEY (OrderId) REFERENCES Orders(Id)
        );
        CREATE TABLE Inventory (
            ProductName TEXT PRIMARY KEY,
            StockCount INTEGER NOT NULL
        );
        """;

    private const string Data_CreateSchema_2 = """
        INSERT INTO Inventory VALUES ('Widget', 100);
        INSERT INTO Inventory VALUES ('Gadget', 50);
        INSERT INTO Inventory VALUES ('Doohickey', 25);
        """;
}
