// Implements [CI-DESLOP-INDEPENDENT-DATA].
namespace Nimblesite.DataProvider.Tests;

public partial class SqlModelE2ETests
{
    private const string Data_CreateSchemaAndSeed_1 = """
        CREATE TABLE Employees (
            Id TEXT PRIMARY KEY,
            Name TEXT NOT NULL,
            Department TEXT NOT NULL,
            Salary REAL NOT NULL,
            YearsExp INTEGER NOT NULL
        );
        CREATE TABLE Departments (
            Name TEXT PRIMARY KEY,
            Budget REAL NOT NULL,
            HeadCount INTEGER NOT NULL
        );
        """;

    private const string Data_CreateSchemaAndSeed_2 = """
        INSERT INTO Employees VALUES ('e1', 'Alice', 'Engineering', 95000, 5);
        INSERT INTO Employees VALUES ('e2', 'Bob', 'Engineering', 105000, 8);
        INSERT INTO Employees VALUES ('e3', 'Charlie', 'Sales', 75000, 3);
        INSERT INTO Employees VALUES ('e4', 'Diana', 'Sales', 85000, 6);
        INSERT INTO Employees VALUES ('e5', 'Eve', 'Marketing', 70000, 2);
        INSERT INTO Employees VALUES ('e6', 'Frank', 'Engineering', 120000, 12);
        INSERT INTO Employees VALUES ('e7', 'Grace', 'Marketing', 80000, 4);
        INSERT INTO Employees VALUES ('e8', 'Hank', 'Sales', 90000, 7);
        INSERT INTO Departments VALUES ('Engineering', 500000, 3);
        INSERT INTO Departments VALUES ('Sales', 300000, 3);
        INSERT INTO Departments VALUES ('Marketing', 200000, 2);
        """;
}
